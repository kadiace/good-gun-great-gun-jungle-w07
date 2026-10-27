using System.Collections.Generic;

using TMPro;

using UnityEngine;

namespace WeaponWorkshop
{
    // 습격 목표·적·탈출 마커의 화면 위치와 문구 표시만 담당한다.
    public sealed class RaidObjectiveUI : MonoBehaviour
    {
        private static readonly Color ENEMY_MARKER_COLOR = new Color(1f, .06f, .08f, 1f);
        private static readonly Color EXIT_MARKER_COLOR = new Color(.35f, 1f, .6f);

        [SerializeField] private FirstPersonPlayer player;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI status;
        [SerializeField] private TextMeshProUGUI markerTemplate;
        [SerializeField] private NightRaidBoundary[] exits;

        private readonly List<TextMeshProUGUI> m_objectiveMarkers = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> m_enemyMarkers = new List<TextMeshProUGUI>();
        private TextMeshProUGUI m_exitMarker;
        private RaidObjectivePresenter m_presenter;
        private bool m_hasLoggedReferenceError;

        private void Awake()
        {
            HidePanel();
            if (markerTemplate != null)
            {
                markerTemplate.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (player == null || viewCamera == null || panel == null || status == null || markerTemplate == null)
            {
                if (!m_hasLoggedReferenceError)
                {
                    m_hasLoggedReferenceError = true;
                    Debug.LogError(
                        "[RaidObjectiveUI] 필수 참조 누락: player, viewCamera, panel, status, markerTemplate를 확인하세요.", this);
                }

                return;
            }

            m_presenter = new RaidObjectivePresenter(this, player, exits);
        }

        private void LateUpdate()
        {
            m_presenter?.Tick();
        }

        private void OnDisable()
        {
            HidePanel();
        }

        public void ShowPanel()
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
            }
        }

        public void HidePanel()
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        public void SetStatus(string _text)
        {
            if (status != null)
            {
                status.text = _text ?? string.Empty;
            }
        }

        public void EnsureObjectiveMarkers(int _count)
        {
            while (m_objectiveMarkers.Count < _count)
            {
                m_objectiveMarkers.Add(Instantiate(markerTemplate, panel));
            }
        }

        public void ShowObjectiveMarker(int _index, bool _visible, Vector3 _world, string _title, bool _refreshText)
        {
            if (_index < 0 || _index >= m_objectiveMarkers.Count)
            {
                return;
            }

            TextMeshProUGUI marker = m_objectiveMarkers[_index];
            marker.gameObject.SetActive(_visible);
            if (_visible)
            {
                PositionMarker(marker, _world, _title, _refreshText);
            }
        }

        public void EnsureExitMarker()
        {
            if (m_exitMarker != null)
            {
                return;
            }

            m_exitMarker = Instantiate(markerTemplate, panel);
            m_exitMarker.color = EXIT_MARKER_COLOR;
        }

        public void ShowExitMarker(bool _visible, Vector3 _world, string _title, bool _refreshText)
        {
            if (m_exitMarker == null)
            {
                return;
            }

            m_exitMarker.gameObject.SetActive(_visible);
            if (_visible)
            {
                PositionMarker(m_exitMarker, _world, _title, _refreshText);
            }
        }

        public void ShowEnemyMarker(int _index, Vector3 _world, string _title, bool _refreshText)
        {
            while (m_enemyMarkers.Count <= _index)
            {
                TextMeshProUGUI marker = Instantiate(markerTemplate, panel);
                marker.color = ENEMY_MARKER_COLOR;
                marker.gameObject.SetActive(false);
                m_enemyMarkers.Add(marker);
            }

            TextMeshProUGUI activeMarker = m_enemyMarkers[_index];
            activeMarker.gameObject.SetActive(true);
            PositionMarker(activeMarker, _world, _title, _refreshText);
        }

        public void HideEnemyMarkersFrom(int _index)
        {
            for (int i = _index; i < m_enemyMarkers.Count; i++)
            {
                m_enemyMarkers[i].gameObject.SetActive(false);
            }
        }

        private void PositionMarker(TextMeshProUGUI _marker, Vector3 _world, string _title, bool _refreshText)
        {
            Vector3 viewport = viewCamera.WorldToViewportPoint(_world);
            Vector2 direction = new Vector2(viewport.x - .5f, viewport.y - .5f);
            bool behind = viewport.z <= 0f;
            if (behind)
            {
                direction = -direction;
                if (direction.sqrMagnitude < .001f)
                {
                    direction = Vector2.down;
                }
            }

            Vector2 half = panel.rect.size * .5f;
            Vector2 limit = new Vector2(Mathf.Max(1f, half.x - 116.25f), Mathf.Max(1f, half.y - 78.75f));
            Vector2 point = Vector2.Scale(direction, panel.rect.size);
            float edge = Mathf.Max(Mathf.Abs(point.x) / limit.x, Mathf.Abs(point.y) / limit.y);
            bool offscreen = behind || edge > 1f;
            if (offscreen)
            {
                point /= Mathf.Max(edge, .001f);
            }

            _marker.rectTransform.anchoredPosition = point;
            if (!_refreshText)
            {
                return;
            }

            float distance = Vector3.Distance(player.transform.position, _world);
            float height = _world.y - player.transform.position.y;
            string elevation = height > 3f ? $" · 위 {height:0}m" :
                height < -3f ? $" · 아래 {-height:0}m" : string.Empty;
            string arrow = offscreen
                ? (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                    ? (direction.x > 0 ? "→ " : "← ")
                    : (direction.y > 0 ? "↑ " : "↓ "))
                : string.Empty;
            _marker.text = $"{arrow}{_title}\n{distance:0}m{elevation}";
        }
    }
}
