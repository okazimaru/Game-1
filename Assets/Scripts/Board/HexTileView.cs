using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HexTileView : MonoBehaviour
{
    public Vector2Int coord;
    public TileOwner owner = TileOwner.Neutral;

    SpriteRenderer sr;
    GameObject hintOverlay;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        // 子から HintOverlay を探す
        var t = transform.Find("HintOverlay");
        if (t != null) hintOverlay = t.gameObject;

        ApplyColor();
        SetHint(false);
    }

    public void SetOwner(TileOwner newOwner)
    {
        owner = newOwner;
        ApplyColor();
    }

    public void SetHint(bool on)
    {
        if (hintOverlay != null)
            hintOverlay.SetActive(on);
    }

    void ApplyColor()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();

        switch (owner)
        {
            case TileOwner.Neutral: sr.color = Color.white; break;
            case TileOwner.Player: sr.color = Color.cyan; break;
            case TileOwner.Enemy: sr.color = Color.red; break;
        }
    }
}