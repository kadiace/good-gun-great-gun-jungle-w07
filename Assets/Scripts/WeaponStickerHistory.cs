using System.Collections.Generic;
using UnityEngine;

namespace WeaponWorkshop
{
    [DisallowMultipleComponent]
    public sealed class WeaponStickerHistory : MonoBehaviour
    {
        public const int MaximumStickerCount = 10;

        private struct StickerEdit
        {
            public StickerOverlay sticker;
            public bool removed;
        }

        private const float SurfaceOffset = 0.0008f;

        private readonly List<StickerOverlay> placedStickers = new List<StickerOverlay>();
        private readonly List<StickerEdit> undoHistory = new List<StickerEdit>();

        public int ActiveStickerCount
        {
            get
            {
                int count = 0;
                foreach (StickerOverlay sticker in placedStickers)
                    if (sticker != null && sticker.gameObject.activeSelf) count++;
                return count;
            }
        }

        public StickerOverlay LastActiveSticker
        {
            get
            {
                for (int i = placedStickers.Count - 1; i >= 0; i--)
                {
                    StickerOverlay sticker = placedStickers[i];
                    if (sticker != null && sticker.gameObject.activeSelf) return sticker;
                }
                return null;
            }
        }

        /// <summary>
        /// Places one sticker on a block surface and records it in the existing undo history.
        /// Surface orientation, sizeMultiplier, and flipHorizontally determine its readable footprint.
        /// </summary>
        public bool Place(PlacedBlock block, Texture2D texture, Vector3 surfacePoint, Vector3 surfaceNormal,
            Vector3 preferredUp, Vector3 fallbackUp, float sizeMultiplier = StickerOverlay.DefaultSizeMultiplier,
            bool flipHorizontally = false)
        {
            if (block == null || texture == null || surfaceNormal.sqrMagnitude < 0.000001f ||
                ActiveStickerCount >= MaximumStickerCount) return false;

            Vector3 normal = surfaceNormal.normalized;
            Vector3 up = Vector3.ProjectOnPlane(preferredUp, normal);
            if (up.sqrMagnitude < 0.000001f) up = Vector3.ProjectOnPlane(fallbackUp, normal);
            if (up.sqrMagnitude < 0.000001f) up = Vector3.ProjectOnPlane(Vector3.up, normal);
            Quaternion rotation = Quaternion.LookRotation(normal, up.normalized);

            var stickerObject = new GameObject("Sticker - " + texture.name);
            stickerObject.transform.SetParent(block.transform, false);
            stickerObject.transform.SetPositionAndRotation(surfacePoint + normal * SurfaceOffset, rotation);
            StickerOverlay overlay = stickerObject.AddComponent<StickerOverlay>();
            if (!overlay.Configure(texture, sizeMultiplier, flipHorizontally))
            {
                if (Application.isPlaying) Destroy(stickerObject);
                else DestroyImmediate(stickerObject);
                return false;
            }

            placedStickers.Add(overlay);
            undoHistory.Add(new StickerEdit { sticker = overlay, removed = false });
            return true;
        }

        public bool ToggleLastStickerHorizontalFlip()
        {
            StickerOverlay sticker = LastActiveSticker;
            return sticker != null && sticker.ToggleHorizontalFlip();
        }

        public bool AdjustLastStickerSize(int direction)
        {
            StickerOverlay sticker = LastActiveSticker;
            return sticker != null && sticker.AdjustSize(direction);
        }

        public bool Remove(StickerOverlay sticker)
        {
            if (sticker == null || !sticker.gameObject.activeSelf || !placedStickers.Contains(sticker)) return false;
            sticker.gameObject.SetActive(false);
            undoHistory.Add(new StickerEdit { sticker = sticker, removed = true });
            return true;
        }

        public void ClearAll()
        {
            StickerOverlay[] stickers = placedStickers.ToArray();
            placedStickers.Clear();
            undoHistory.Clear();
            foreach (StickerOverlay sticker in stickers)
            {
                if (sticker == null) continue;
                if (Application.isPlaying) Destroy(sticker.gameObject);
                else DestroyImmediate(sticker.gameObject);
            }
        }

        public bool Undo()
        {
            while (undoHistory.Count > 0)
            {
                int last = undoHistory.Count - 1;
                StickerEdit edit = undoHistory[last];
                undoHistory.RemoveAt(last);
                StickerOverlay sticker = edit.sticker;
                if (sticker == null) continue;
                if (edit.removed)
                {
                    sticker.gameObject.SetActive(true);
                    return true;
                }

                placedStickers.Remove(sticker);
                if (Application.isPlaying) Destroy(sticker.gameObject);
                else DestroyImmediate(sticker.gameObject);
                return true;
            }
            return false;
        }

        private void OnTransformChildrenChanged()
        {
            placedStickers.RemoveAll(sticker => sticker == null);
            undoHistory.RemoveAll(edit => edit.sticker == null);
        }
    }
}
