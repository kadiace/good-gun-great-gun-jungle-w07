using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 좀보이드 타일 명명 규칙을 기반으로 카테고리를 분류하고 3D 지오메트리 및 충돌 속성을 결정하는 클래스입니다.
    /// </summary>
    public static class PZTileClassifier
    {
        private static readonly HashSet<string> _unknownTilesLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static int UnknownTilesCount => _unknownTilesLogged.Count;

        /// <summary>
        /// 미분류 타일 로그 집합을 초기화합니다.
        /// </summary>
        public static void ResetUnknownLog()
        {
            _unknownTilesLogged.Clear();
        }

        /// <summary>
        /// 타일 이름을 검사하여 구조적 카테고리와 방향 서브타입을 판별합니다.
        /// </summary>
        /// <param name="tileName">타일 에셋 이름</param>
        /// <returns>카테고리와 서브타입 튜플</returns>
        public static (TileCategory category, TileSubType subType) Classify(string tileName)
        {
            if (string.IsNullOrEmpty(tileName))
            {
                return (TileCategory.Unknown, TileSubType.Unknown);
            }

            int lastUnderscore = tileName.LastIndexOf('_');
            string prefix = tileName;
            int index = 0;
            if (lastUnderscore > 0 && int.TryParse(tileName.Substring(lastUnderscore + 1), out int parsedIdx))
            {
                prefix = tileName.Substring(0, lastUnderscore);
                index = parsedIdx;
            }

            string p = prefix.ToLowerInvariant();

            // 1. 벽 (Walls)
            if (p.StartsWith("walls_exterior") || p.StartsWith("walls_interior") || p.StartsWith("walls_garage") ||
                p.StartsWith("walls_commercial") || p.StartsWith("walls_shed") || p.StartsWith("walls_decoration"))
            {
                int mod = index % 4;
                TileSubType sub = mod switch
                {
                    0 => TileSubType.WestWall,
                    1 => TileSubType.NorthWall,
                    2 => TileSubType.CornerWall,
                    _ => TileSubType.WallPost
                };
                return (TileCategory.Wall, sub);
            }

            // 2. 창문 (Windows)
            if (p.Contains("window") || p.StartsWith("fixtures_windows"))
            {
                TileSubType sub = (index % 4 == 0) ? TileSubType.WestWindow : TileSubType.NorthWindow;
                return (TileCategory.Window, sub);
            }

            // 3. 문 및 문틀 (Doors & Door Frames)
            if (p.Contains("door") || p.StartsWith("fixtures_doors"))
            {
                TileSubType sub = (index % 4 == 0) ? TileSubType.WestDoor : TileSubType.NorthDoor;
                return (TileCategory.Door, sub);
            }

            // 4. 지붕 (Roofs)
            if (p.StartsWith("roofs") || p.Contains("roof"))
            {
                return (TileCategory.Roof, TileSubType.RoofSlab);
            }

            // 5. 울타리 (Fences)
            if (p.StartsWith("fencing") || p.StartsWith("industry_fencing"))
            {
                TileSubType sub = (index % 4 == 0) ? TileSubType.WestFence : TileSubType.NorthFence;
                return (TileCategory.Fence, sub);
            }

            // 6. 실내 바닥 (Floors - Interior)
            if (p.StartsWith("floors_interior"))
            {
                return (TileCategory.Floor, TileSubType.InteriorFloor);
            }

            // 7. 실외 바닥 (Floors - Exterior)
            if (p.StartsWith("floors_exterior") && !p.Contains("street"))
            {
                return (TileCategory.Floor, TileSubType.ExteriorFloor);
            }

            // 8. 도로 (Roads, Curbs, Traffic Lines)
            if (p.StartsWith("blends_street") || p.StartsWith("floors_exterior_street"))
            {
                return (TileCategory.Road, TileSubType.Asphalt);
            }
            if (p.StartsWith("street_curbs"))
            {
                return (TileCategory.Road, TileSubType.Curb);
            }
            if (p.StartsWith("street_trafficlines"))
            {
                return (TileCategory.Road, TileSubType.TrafficLine);
            }

            // 9. 식생 및 자연 (Vegetation & Natural)
            if (p.StartsWith("vegetation_trees") || p.StartsWith("jumbo_tree"))
            {
                return (TileCategory.Vegetation, TileSubType.Tree);
            }
            if (p.StartsWith("vegetation_foliage") || p.StartsWith("d_plants"))
            {
                return (TileCategory.Vegetation, TileSubType.Bush);
            }
            if (p.StartsWith("blends_natural") || p.StartsWith("blends_grassoverlays"))
            {
                if (p.Contains("water")) return (TileCategory.Water, TileSubType.Water);
                if (p.Contains("dirt") || (index >= 16 && index <= 23)) return (TileCategory.Vegetation, TileSubType.Dirt);
                return (TileCategory.Vegetation, TileSubType.Grass);
            }

            // 10. 가구 및 비품 (Furniture & Fixtures)
            if (p.StartsWith("furniture") || p.StartsWith("fixtures") || p.StartsWith("appliances"))
            {
                if (p.Contains("bed")) return (TileCategory.Furniture, TileSubType.Bed);
                if (p.Contains("storage")) return (TileCategory.Furniture, TileSubType.Wardrobe);
                if (p.Contains("counter")) return (TileCategory.Furniture, TileSubType.Counter);
                if (p.Contains("seating") || p.Contains("chair")) return (TileCategory.Furniture, TileSubType.Seating);
                if (p.Contains("table")) return (TileCategory.Furniture, TileSubType.Table);
                if (p.Contains("bathroom") || p.Contains("sink") || p.Contains("toilet")) return (TileCategory.Furniture, TileSubType.Bathroom);
                return (TileCategory.Furniture, TileSubType.Furniture);
            }

            // 11. 장식, 커튼, 몰딩 디테일, 오버레이, 쓰레기 (오분류 수정: 단단한 장애물이 되지 않도록 처리)
            if (p.StartsWith("curtains") || p.StartsWith("detailing") || p.StartsWith("trash") ||
                p.StartsWith("overlay") || p.StartsWith("location") || p.StartsWith("lighting") || p.StartsWith("signage"))
            {
                return (TileCategory.Decoration, TileSubType.Overlay);
            }

            if (_unknownTilesLogged.Add(tileName))
            {
                // 미분류 타일은 콘솔 로그에 한 번만 기록합니다.
            }

            return (TileCategory.Unknown, TileSubType.Unknown);
        }

        /// <summary>
        /// 건물 용도 및 타일 카테고리에 맞는 기본 색상을 반환합니다.
        /// </summary>
        /// <param name="category">타일 카테고리</param>
        /// <param name="subType">서브타입</param>
        /// <param name="buildingType">건물 용도 태그</param>
        /// <returns>적용할 기본 색상</returns>
        public static Color GetCategoryColor(TileCategory category, TileSubType subType, string buildingType = "")
        {
            string b = buildingType.ToLowerInvariant();

            if (category == TileCategory.Wall)
            {
                if (b.Contains("gunstore"))
                {
                    return new Color(0.38f, 0.42f, 0.36f);
                }
                if (b.Contains("medclinic") || b.Contains("hospital"))
                {
                    return new Color(0.88f, 0.92f, 0.94f);
                }
                if (b.Contains("conveniencestore"))
                {
                    return new Color(0.82f, 0.78f, 0.68f);
                }
                if (b.Contains("office"))
                {
                    return new Color(0.55f, 0.58f, 0.62f);
                }
                if (b.Contains("shed") || b.Contains("storage"))
                {
                    return new Color(0.45f, 0.42f, 0.40f);
                }
                return new Color(0.76f, 0.70f, 0.64f);
            }

            return category switch
            {
                TileCategory.Window => new Color(0.40f, 0.75f, 0.95f, 0.65f),
                TileCategory.Door => new Color(0.55f, 0.35f, 0.20f),
                TileCategory.Roof => new Color(0.32f, 0.30f, 0.28f),
                TileCategory.Floor => subType == TileSubType.InteriorFloor
                    ? new Color(0.80f, 0.67f, 0.53f)
                    : new Color(0.68f, 0.68f, 0.68f),
                TileCategory.Road => subType switch
                {
                    TileSubType.Curb => new Color(0.65f, 0.65f, 0.65f),
                    TileSubType.TrafficLine => new Color(0.95f, 0.85f, 0.20f),
                    _ => new Color(0.22f, 0.22f, 0.24f)
                },
                TileCategory.Vegetation => subType switch
                {
                    TileSubType.Tree => new Color(0.18f, 0.48f, 0.20f),
                    TileSubType.Bush => new Color(0.24f, 0.58f, 0.24f),
                    TileSubType.Dirt => new Color(0.48f, 0.38f, 0.28f),
                    _ => new Color(0.30f, 0.55f, 0.22f)
                },
                TileCategory.Furniture => subType switch
                {
                    TileSubType.Bed => new Color(0.68f, 0.30f, 0.30f),
                    TileSubType.Wardrobe => new Color(0.46f, 0.30f, 0.20f),
                    TileSubType.Counter => new Color(0.72f, 0.70f, 0.66f),
                    TileSubType.Seating => new Color(0.36f, 0.42f, 0.56f),
                    TileSubType.Bathroom => new Color(0.90f, 0.92f, 0.95f),
                    _ => new Color(0.56f, 0.44f, 0.34f)
                },
                TileCategory.Fence => new Color(0.58f, 0.50f, 0.40f),
                TileCategory.Water => new Color(0.15f, 0.45f, 0.75f, 0.80f),
                TileCategory.Decoration => new Color(0.50f, 0.50f, 0.50f, 0.70f),
                _ => new Color(0.70f, 0.70f, 0.70f)
            };
        }

        /// <summary>
        /// 타일의 카테고리, 서브타입, 원본 타일명을 종합하여 3D 지오메트리와 속성을 구성합니다.
        /// </summary>
        /// <param name="category">타일 카테고리</param>
        /// <param name="subType">서브타입</param>
        /// <param name="tileName">타일 이름</param>
        /// <param name="buildingType">건물 용도 태그</param>
        /// <param name="wallHeight">층간 벽 높이</param>
        /// <returns>계산된 프리미티브 지오메트리 구조체</returns>
        public static PrimitiveGeometry GetPrimitiveGeometry(
            TileCategory category,
            TileSubType subType,
            string tileName,
            string buildingType = "",
            float wallHeight = 2.8f)
        {
            Color color = GetCategoryColor(category, subType, buildingType);
            string lowerName = (tileName ?? string.Empty).ToLowerInvariant();

            bool isDoorFrame = lowerName.Contains("fixtures_doors_frames");
            bool isActualDoor = (category == TileCategory.Door) && !isDoorFrame;
            bool isWindow = (category == TileCategory.Window);

            switch (category)
            {
                case TileCategory.Wall:
                    if (subType == TileSubType.WestWall)
                    {
                        return new PrimitiveGeometry
                        {
                            localPosition = new Vector3(0.075f, wallHeight * 0.5f, 0.5f),
                            size = new Vector3(0.15f, wallHeight, 1.0f),
                            rotation = Quaternion.identity,
                            color = color,
                            hasCollider = true
                        };
                    }
                    if (subType == TileSubType.NorthWall)
                    {
                        return new PrimitiveGeometry
                        {
                            localPosition = new Vector3(0.5f, wallHeight * 0.5f, 0.925f),
                            size = new Vector3(1.0f, wallHeight, 0.15f),
                            rotation = Quaternion.identity,
                            color = color,
                            hasCollider = true
                        };
                    }
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.1f, wallHeight * 0.5f, 0.9f),
                        size = new Vector3(0.2f, wallHeight, 0.2f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true
                    };

                case TileCategory.Door:
                    if (isDoorFrame)
                    {
                        // 문틀 타일은 출입구를 차단하지 않도록 충돌체가 없는 개방형 테두리로 생성합니다.
                        bool isWestFrame = (subType == TileSubType.WestDoor);
                        return new PrimitiveGeometry
                        {
                            localPosition = isWestFrame ? new Vector3(0.075f, 2.3f, 0.5f) : new Vector3(0.5f, 2.3f, 0.925f),
                            size = isWestFrame ? new Vector3(0.14f, 0.2f, 1.0f) : new Vector3(1.0f, 0.2f, 0.14f),
                            rotation = Quaternion.identity,
                            color = color * 0.8f,
                            hasCollider = false,
                            isDoorFrame = true
                        };
                    }

                    if (subType == TileSubType.WestDoor)
                    {
                        return new PrimitiveGeometry
                        {
                            localPosition = new Vector3(0.075f, 1.1f, 0.5f),
                            size = new Vector3(0.12f, 2.2f, 0.85f),
                            rotation = Quaternion.identity,
                            color = color,
                            hasCollider = true,
                            isDoor = true
                        };
                    }
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 1.1f, 0.925f),
                        size = new Vector3(0.85f, 2.2f, 0.12f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true,
                        isDoor = true
                    };

                case TileCategory.Window:
                    if (subType == TileSubType.WestWindow)
                    {
                        return new PrimitiveGeometry
                        {
                            localPosition = new Vector3(0.075f, 1.4f, 0.5f),
                            size = new Vector3(0.12f, 1.6f, 0.85f),
                            rotation = Quaternion.identity,
                            color = color,
                            hasCollider = true,
                            isWindow = true
                        };
                    }
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 1.4f, 0.925f),
                        size = new Vector3(0.85f, 1.6f, 0.12f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true,
                        isWindow = true
                    };

                case TileCategory.Floor:
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 0.025f, 0.5f),
                        size = new Vector3(1.0f, 0.05f, 1.0f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true
                    };

                case TileCategory.Roof:
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, wallHeight + 0.1f, 0.5f),
                        size = new Vector3(1.0f, 0.2f, 1.0f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true
                    };

                case TileCategory.Road:
                    float roadH = (subType == TileSubType.Curb) ? 0.15f : 0.02f;
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, roadH * 0.5f, 0.5f),
                        size = new Vector3(1.0f, roadH, 1.0f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = (subType == TileSubType.Curb)
                    };

                case TileCategory.Furniture:
                    float fH = subType switch
                    {
                        TileSubType.Wardrobe => 1.8f,
                        TileSubType.Bed => 0.55f,
                        TileSubType.Counter => 0.9f,
                        TileSubType.Bathroom => 0.85f,
                        _ => 0.8f
                    };
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, fH * 0.5f, 0.5f),
                        size = new Vector3(0.85f, fH, 0.85f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true
                    };

                case TileCategory.Fence:
                    bool isWestFence = (subType == TileSubType.WestFence);
                    return new PrimitiveGeometry
                    {
                        localPosition = isWestFence ? new Vector3(0.05f, 0.6f, 0.5f) : new Vector3(0.5f, 0.6f, 0.95f),
                        size = isWestFence ? new Vector3(0.1f, 1.2f, 1.0f) : new Vector3(1.0f, 1.2f, 0.1f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = true
                    };

                case TileCategory.Vegetation:
                    if (subType == TileSubType.Tree)
                    {
                        return new PrimitiveGeometry
                        {
                            localPosition = new Vector3(0.5f, 2.0f, 0.5f),
                            size = new Vector3(0.8f, 4.0f, 0.8f),
                            rotation = Quaternion.identity,
                            color = color,
                            hasCollider = true
                        };
                    }
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 0.01f, 0.5f),
                        size = new Vector3(1.0f, 0.02f, 1.0f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = false
                    };

                case TileCategory.Decoration:
                    // 장식/커튼/오버레이는 진입을 막지 않도록 얇은 평면 및 충돌체 제거로 처리합니다.
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 0.01f, 0.5f),
                        size = new Vector3(0.9f, 0.01f, 0.9f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = false
                    };

                default:
                    // Unknown 타일도 진입 차단을 막기 위해 충돌체를 두지 않습니다.
                    return new PrimitiveGeometry
                    {
                        localPosition = new Vector3(0.5f, 0.05f, 0.5f),
                        size = new Vector3(0.5f, 0.1f, 0.5f),
                        rotation = Quaternion.identity,
                        color = color,
                        hasCollider = false
                    };
            }
        }
    }
}
