using UnityEngine;

public class ForceTexelSize : MonoBehaviour
{
    void Start()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        Vector2 texelSize = Vector2.zero;
        if ((spriteRenderer) && (spriteRenderer.sprite) && (spriteRenderer.sprite.texture))
        {
            var texture = spriteRenderer.sprite.texture;
            texelSize = new Vector2(1.0f / texture.width, 1.0f / texture.height);
        }

        MaterialPropertyBlock mpb = new();
        spriteRenderer.GetPropertyBlock(mpb);

        mpb.SetVector("_OutlineTexelSize", texelSize);

        spriteRenderer.SetPropertyBlock(mpb);
    }
}
