using System;
using System.Collections.Generic;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 좀보이드 타일의 기능적 및 구조적 카테고리입니다.
    /// </summary>
    public enum TileCategory
    {
        Floor,
        Wall,
        Window,
        Door,
        Roof,
        Stairs,
        Fence,
        Road,
        Vegetation,
        Furniture,
        Decoration,
        Water,
        Unknown
    }

    /// <summary>
    /// 벽 및 구조물의 방향과 세부 형태 서브타입입니다.
    /// </summary>
    public enum TileSubType
    {
        None,
        WestWall,
        NorthWall,
        CornerWall,
        WallPost,
        WestWindow,
        NorthWindow,
        WestDoor,
        NorthDoor,
        RoofSlab,
        WestFence,
        NorthFence,
        InteriorFloor,
        ExteriorFloor,
        Asphalt,
        Curb,
        TrafficLine,
        Tree,
        Bush,
        Grass,
        Dirt,
        Bed,
        Wardrobe,
        Counter,
        Seating,
        Table,
        Bathroom,
        Furniture,
        Overlay,
        Water,
        Unknown
    }

    /// <summary>
    /// 단일 타일 인스턴스 데이터 직렬화 클래스입니다.
    /// </summary>
    [Serializable]
    public class TileInstance
    {
        public int x;
        public int y;
        public int worldTileX;
        public int worldTileY;
        public int z;
        public int layer;
        public string tile;
        public string category;
        public string subType;
        public float[] color;
        public float h;
        public float t;
        public bool collider;
    }

    /// <summary>
    /// 건물 데이터 직렬화 클래스입니다.
    /// </summary>
    [Serializable]
    public class BuildingData
    {
        public string cell;
        public int buildingId;
        public List<string> names;
        public BuildingBounds bounds;
        public List<BuildingRoom> rooms;
        public int tileCount;
        public List<BuildingTile> tiles;
    }

    /// <summary>
    /// 건물의 월드 및 로컬 바운딩 정보입니다.
    /// </summary>
    [Serializable]
    public class BuildingBounds
    {
        public int minX;
        public int minY;
        public int width;
        public int height;
        public int worldX;
        public int worldY;
    }

    /// <summary>
    /// 건물 내 방 정보입니다.
    /// </summary>
    [Serializable]
    public class BuildingRoom
    {
        public int id;
        public string name;
        public int floor;
        public List<RoomRectCoords> rects;
    }

    /// <summary>
    /// 방 직사각형 좌표 정보입니다.
    /// </summary>
    [Serializable]
    public class RoomRectCoords
    {
        public int x;
        public int y;
        public int w;
        public int h;
    }

    /// <summary>
    /// 건물 내 배치 타일의 원시 데이터입니다.
    /// </summary>
    [Serializable]
    public class BuildingTile
    {
        public int lx;
        public int ly;
        public int cellX;
        public int cellY;
        public int z;
        public int layer;
        public string tile;
        public string cat;
        public string sub;
        public float[] col;
    }

    /// <summary>
    /// 3D 프리미티브 지오메트리 정보 구조체입니다.
    /// </summary>
    public struct PrimitiveGeometry
    {
        public Vector3 localPosition;
        public Vector3 size;
        public Quaternion rotation;
        public Color color;
        public bool hasCollider;
        public bool isDoor;
        public bool isWindow;
        public bool isDoorFrame;
    }
}
