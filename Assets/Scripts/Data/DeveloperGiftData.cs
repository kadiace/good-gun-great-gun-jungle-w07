using System;
using UnityEngine;

namespace WeaponWorkshop
{
    [Serializable]
    public sealed class DeveloperGiftData
    {
        public string weaponCode;
        public string basePaintMaterialId;
        public DeveloperGiftPaintOverrideData[] paintOverrides;
        public string stickerId;
        public DeveloperGiftStickerData[] stickers;
    }

    [Serializable]
    public sealed class DeveloperGiftPaintOverrideData
    {
        public string materialId;
        public Vector3Int[] cells;
        public Vector3Int[] faces;
    }

    [Serializable]
    public sealed class DeveloperGiftStickerData
    {
        public Vector3Int[] cells;
        public Vector3Int face;
        public float sizeMultiplier = 1f;
        public bool flipHorizontally;
    }
}
