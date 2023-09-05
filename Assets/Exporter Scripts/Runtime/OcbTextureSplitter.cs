using UnityEngine;

namespace UnityTextureSplitter
{

    [CreateAssetMenu(fileName = "Texture", menuName = "Texture Splitter", order = 99)]
    [System.Serializable]
    public class OcbTextureSplitter : ScriptableObject
    {

        [HideInInspector]
        public int TextureSplitWidth = 4;
        [HideInInspector]
        public int TextureSplitHeight = 4;

        [HideInInspector]
        public int TextureOffsetWidth = 0;
        [HideInInspector]
        public int TextureOffsetHeight = 0;

        [HideInInspector]
        public Texture2D TextureSource;

    }

}

