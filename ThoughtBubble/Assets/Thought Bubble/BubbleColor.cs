using UnityEngine;

namespace ThoughtBubble
{
    public class BubbleColor : MonoBehaviour
    {
        [SerializeField] private Color _color = Color.white;

        private static readonly int TintID = Shader.PropertyToID("_Tint");
        private Renderer _renderer;

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                Apply();
            }
        }

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            Apply();
        }

        private void Apply()
        {
            if (_renderer != null)
                _renderer.material.SetColor(TintID, _color);
        }
    }
}
