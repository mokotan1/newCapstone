using TMPro;
using UnityEngine;

/// <summary>
/// Adds subtle per-glyph position and rotation variance while keeping the title readable.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class HorrorTitleCharacterJitter : MonoBehaviour
{
    [SerializeField] int seed = HorrorTitleTypography.DefaultJitterSeed;
    [SerializeField] float positionJitter = HorrorTitleTypography.PositionJitter;
    [SerializeField] float rotationJitterDegrees = HorrorTitleTypography.RotationJitterDegrees;

    TMP_Text text;
    bool meshDirty;

    public void Configure(int jitterSeed, float positionAmount, float rotationDegrees)
    {
        seed = jitterSeed;
        positionJitter = positionAmount;
        rotationJitterDegrees = rotationDegrees;
        meshDirty = true;
    }

    void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        meshDirty = true;
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    void LateUpdate()
    {
        if (!meshDirty || text == null)
            return;

        text.ForceMeshUpdate();
        ApplyVertexJitter();
        meshDirty = false;
    }

    void OnTextChanged(Object changedObject)
    {
        if (changedObject == text)
            meshDirty = true;
    }

    void ApplyVertexJitter()
    {
        TMP_TextInfo textInfo = text.textInfo;
        if (textInfo == null || textInfo.characterCount == 0)
            return;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
            Vector2 offset = SampleOffset(i);
            float rotation = SampleRotation(i);

            for (int v = 0; v < 4; v++)
            {
                Vector3 vertex = vertices[vertexIndex + v] - center;
                vertex = Quaternion.Euler(0f, 0f, rotation) * vertex;
                vertex += center;
                vertex.x += offset.x;
                vertex.y += offset.y;
                vertices[vertexIndex + v] = vertex;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }

    Vector2 SampleOffset(int characterIndex)
    {
        var random = new System.Random(unchecked(seed + characterIndex * 7919));
        float x = ((float)random.NextDouble() * 2f - 1f) * positionJitter;
        float y = ((float)random.NextDouble() * 2f - 1f) * positionJitter * 0.55f;
        return new Vector2(x, y);
    }

    float SampleRotation(int characterIndex)
    {
        var random = new System.Random(unchecked(seed + characterIndex * 7907 + 17));
        return ((float)random.NextDouble() * 2f - 1f) * rotationJitterDegrees;
    }
}
