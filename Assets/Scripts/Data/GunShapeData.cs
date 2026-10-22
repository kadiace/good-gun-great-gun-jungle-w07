using System;
namespace WeaponWorkshop
{
    // 직렬화만 담당한다. 영역 조회·판정은 GunShapeInfo에 둔다.
    [Serializable] public sealed class GunShapeData
    {
        [Serializable] public sealed class Shape
        {
            public string id, name, mode, plane;
            // bounds: 코어 중심의 X/Y/Z 크기. 짝수 크기는 음수 방향에 한 칸 더 둔다.
            public int width, height, depth;
            // silhouette: 열=Z, 첫 행=가장 높은 Y. #/?는 허용 셀, .는 금지 셀.
            public string[] rows;
            public int anchorColumn, anchorRow;
            public bool allowMirrorZ;
            public int toleranceCells;
            public float toleranceRatio;
            [NonSerialized] public bool valid;
        }
        public Shape[] shapes = Array.Empty<Shape>();
    }
}
