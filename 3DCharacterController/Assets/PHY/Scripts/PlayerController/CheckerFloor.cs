using UnityEngine;

public class CheckerFloor : MonoBehaviour
{
    [Header("플레인 크기 (m)")]
    [SerializeField] float width = 20f;
    [SerializeField] float depth = 20f;

    [Header("타일 설정")]
    [SerializeField] Color colorA = new Color(0.30f, 0.30f, 0.30f);
    [SerializeField] Color colorB = new Color(0.55f, 0.55f, 0.55f);
    [SerializeField] float tileSize = 1f; // 타일 한 칸의 월드 크기(m)

    void Awake()
    {
        ApplyFloor();
    }

    // 인스펙터에서 값을 바꿀 때 에디터 뷰에서도 크기와 타일 비율 미리보기
    void OnValidate()
    {
        width = Mathf.Max(0.1f, width);
        depth = Mathf.Max(0.1f, depth);
        tileSize = Mathf.Max(0.01f, tileSize);

        // 기본 Plane은 10x10m이므로 10으로 나눔 (Cube/Quad라면 10f를 1f로 변경)
        transform.localScale = new Vector3(width / 10f, transform.localScale.y, depth / 10f);

        // 에디터 실행 중이 아닐 때도 머티리얼 타일링 반영
        var renderer = GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Vector2 tiling = new Vector2(width / (tileSize * 2f), depth / (tileSize * 2f));
            if (Application.isPlaying)
                renderer.material.mainTextureScale = tiling;
            else
                renderer.sharedMaterial.mainTextureScale = tiling;
        }
    }

    public void ApplyFloor()
    {
        transform.localScale = new Vector3(width / 10f, transform.localScale.y, depth / 10f);

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(new[] { colorA, colorB, colorB, colorA });
        tex.Apply();

        var mat = GetComponent<Renderer>().material;
        mat.mainTexture = tex;
        mat.mainTextureScale = new Vector2(width / (tileSize * 2f), depth / (tileSize * 2f));
    }
}

