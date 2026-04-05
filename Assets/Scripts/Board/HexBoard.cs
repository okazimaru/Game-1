using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HexBoard : MonoBehaviour
{
    [Header("Assign HexTile prefab here")]
    public GameObject hexTilePrefab;

    [Header("Board radius (4 => 61 tiles)")]
    public int radius = 4;

    [Header("UI / Debug")]
    public bool showTurnUI = true;
    public bool logOnPlace = true;

    Dictionary<Vector2Int, HexTileView> tiles = new Dictionary<Vector2Int, HexTileView>();

    TileOwner turn = TileOwner.Player;

    List<HexTileView> legalMoves = new List<HexTileView>();

    static readonly Vector2Int[] DIRS = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1),
    };

    void Start()
    {
        GenerateBoard();
        SetupInitialStones();

        Debug.Log($"Start: tiles={tiles.Count}, turn={turn}");
        Debug.Log($"LegalMoves(Player)={CountLegalMoves(TileOwner.Player)}  LegalMoves(Enemy)={CountLegalMoves(TileOwner.Enemy)}");
        UpdateLegalHints();
    }

    void Update()
    {
        if (gameOver) return;
        if (Mouse.current == null || Camera.main == null) return;

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // screen -> world
        var sp = Mouse.current.position.ReadValue();
        float z = -Camera.main.transform.position.z;
        var wp = Camera.main.ScreenToWorldPoint(new Vector3(sp.x, sp.y, z));
        wp.z = 0f;

        var hit = Physics2D.Raycast(wp, Vector2.zero);
        if (hit.collider == null) return;

        var tile = hit.collider.GetComponent<HexTileView>();
        if (tile == null) return;

        // 置けなかったら理由ログ
        if (!TryPlace(tile, turn, out string reason))
        {
            if (logOnPlace) Debug.Log($"Blocked ({turn}) at {tile.coord}: {reason}");
            return;
        }

        // 盤面が埋まったら終了（優先）
        if (CountEmpty() == 0)
        {
            EndGame("board filled");
            UpdateLegalHints(); // 一応
            return;
        }

        // 手番交代
        turn = (turn == TileOwner.Player) ? TileOwner.Enemy : TileOwner.Player;

        // 次手番が置けないならパス
        if (CountLegalMoves(turn) == 0)
        {
            if (logOnPlace) Debug.Log($"PASS: {turn} has no legal moves.");

            // もう一回戻す
            turn = (turn == TileOwner.Player) ? TileOwner.Enemy : TileOwner.Player;

            // 両者置けないなら終了
            if (CountLegalMoves(turn) == 0)
            {
                EndGame("no legal moves for both");
                UpdateLegalHints(); // 一応
                return;
            }
        }

        if (logOnPlace)
            Debug.Log($"Turn -> {turn} (PlayerMoves={CountLegalMoves(TileOwner.Player)}, EnemyMoves={CountLegalMoves(TileOwner.Enemy)})");

        // 手番が確定した後に更新
        UpdateLegalHints();
    }

    void UpdateLegalHints()
    {
        // 前回のヒントを消す
        foreach (var t in legalMoves) t.SetHint(false);
        legalMoves.Clear();

        // 現在手番の合法手だけ点灯
        foreach (var kv in tiles)
        {
            var tile = kv.Value;
            if (tile.owner != TileOwner.Neutral) continue;

            if (GetFlips(tile.coord, turn).Count > 0)
            {
                legalMoves.Add(tile);
                tile.SetHint(true);
            }
        }
    }

    void OnGUI()
    {
        if (!showTurnUI) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, gameOver ? 220 : 140), GUI.skin.box);

        GUILayout.Label($"Turn: {turn}");
        GUILayout.Label($"PlayerMoves: {CountLegalMoves(TileOwner.Player)}");
        GUILayout.Label($"EnemyMoves : {CountLegalMoves(TileOwner.Enemy)}");
        GUILayout.Label($"PlayerStones: {CountStones(TileOwner.Player)}");
        GUILayout.Label($"EnemyStones : {CountStones(TileOwner.Enemy)}");

        if (gameOver)
        {
            GUILayout.Space(10);
            GUILayout.Label($"RESULT: {resultText}");
            if (GUILayout.Button("Restart"))
                RestartGame();
        }

        GUILayout.EndArea();
    }

    // ===== Othello Rule =====

    bool TryPlace(HexTileView tile, TileOwner placer, out string reason)
    {
        reason = "";

        if (tile.owner != TileOwner.Neutral)
        {
            reason = $"not neutral (owner={tile.owner})";
            return false;
        }

        var flips = GetFlips(tile.coord, placer);
        if (flips.Count == 0)
        {
            reason = "flips=0 (no capture)";
            return false;
        }

        tile.SetOwner(placer);
        foreach (var t in flips) t.SetOwner(placer);

        if (logOnPlace) Debug.Log($"Placed {placer} at {tile.coord}, flips={flips.Count}");
        return true;
    }

    List<HexTileView> GetFlips(Vector2Int start, TileOwner placer)
    {
        var result = new List<HexTileView>();
        var opp = (placer == TileOwner.Player) ? TileOwner.Enemy : TileOwner.Player;

        foreach (var dir in DIRS)
        {
            var line = new List<HexTileView>();
            var cur = start + dir;

            while (tiles.TryGetValue(cur, out var t) && t.owner == opp)
            {
                line.Add(t);
                cur += dir;
            }

            if (line.Count > 0 && tiles.TryGetValue(cur, out var end) && end.owner == placer)
            {
                result.AddRange(line);
            }
        }

        return result;
    }

    int CountLegalMoves(TileOwner side)
    {
        int count = 0;
        foreach (var kv in tiles)
        {
            var tile = kv.Value;
            if (tile.owner != TileOwner.Neutral) continue;
            if (GetFlips(tile.coord, side).Count > 0) count++;
        }
        return count;
    }

    // ===== Setup =====

    void SetupInitialStones()
    {
        ForceSet(new Vector2Int(0, 0), TileOwner.Player);
        ForceSet(new Vector2Int(1, 0), TileOwner.Enemy);
        ForceSet(new Vector2Int(0, 1), TileOwner.Enemy);
        ForceSet(new Vector2Int(-1, 1), TileOwner.Player);
    }

    void ForceSet(Vector2Int coord, TileOwner owner)
    {
        if (tiles.TryGetValue(coord, out var t))
            t.SetOwner(owner);
    }

    // ===== Board Generation =====

    void GenerateBoard()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        tiles.Clear();

        for (int q = -radius; q <= radius; q++)
        {
            for (int r = -radius; r <= radius; r++)
            {
                int s = -q - r;
                if (Mathf.Abs(q) > radius) continue;
                if (Mathf.Abs(r) > radius) continue;
                if (Mathf.Abs(s) > radius) continue;

                var coord = new Vector2Int(q, r);
                var pos = AxialToWorld(q, r);

                var go = Instantiate(hexTilePrefab, pos, Quaternion.identity, transform);
                go.name = $"Hex_{q}_{r}";

                var view = go.GetComponent<HexTileView>();
                if (view == null)
                {
                    Debug.LogError("HexTile prefab has no HexTileView component.");
                    continue;
                }
                view.coord = coord;
                view.SetOwner(TileOwner.Neutral);
                tiles[coord] = view;
            }
        }
    }

    Vector3 AxialToWorld(int q, int r)
    {
        float x = 1.7320508f * (q + r * 0.5f);
        float y = 1.5f * r;
        return new Vector3(x, y, 0);
    }

    int CountStones(TileOwner who)
    {
        int c = 0;
        foreach (var kv in tiles)
            if (kv.Value.owner == who) c++;
        return c;
    }

    int CountEmpty()
    {
        int c = 0;
        foreach (var kv in tiles)
            if (kv.Value.owner == TileOwner.Neutral) c++;
        return c;
    }

    bool gameOver = false;
    string resultText = "";

    void EndGame(string reason)
    {
        gameOver = true;

        int ps = CountStones(TileOwner.Player);
        int es = CountStones(TileOwner.Enemy);

        if (ps > es) resultText = $"Player WIN {ps}-{es}";
        else if (es > ps) resultText = $"Enemy WIN {ps}-{es}";
        else resultText = $"DRAW {ps}-{es}";

        Debug.Log($"GAME OVER ({reason}): {resultText}");
    }

    void RestartGame()
    {
        gameOver = false;
        resultText = "";
        turn = TileOwner.Player;

        GenerateBoard();
        SetupInitialStones();
        UpdateLegalHints();

        Debug.Log("Restarted game.");
    }
}
