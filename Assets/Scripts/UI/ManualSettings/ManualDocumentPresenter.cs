using System;

using UnityEngine;

namespace WeaponWorkshop
{
    /// <summary>
    /// 매뉴얼 종이의 카메라 정렬 거리·줌·팬 계산과 위치 보간 값을 담당하는 Presenter입니다.
    /// 실제 Transform 적용과 종이 활성·콜라이더 상태는 WorkshopManual View가 소유합니다.
    /// </summary>
    public sealed class ManualDocumentPresenter : IDisposable
    {
        private const string PRESENTER_NAME = "ManualDocumentPresenter";
        private const float FIT_SCALE = 1.5f;
        private const float MIN_ASPECT = .8f;
        private const float NEAR_CLIP_MARGIN = .12f;
        private const float POSITION_BLEND_RATE = 18f;
        private const float ZOOM_STEP = .15f;
        private const float ZOOM_MIN = .75f;
        private const float ZOOM_MAX = 3f;
        private const float PAN_LIMIT_RATIO = .45f;

        private readonly WorkshopManual m_view;
        private readonly float m_documentHeight;
        private readonly Vector3 m_restingScale;

        private float m_zoom = 1f;
        private Vector2 m_pan;
        private bool m_isInitialized;
        private bool m_isDisposed;

        public ManualDocumentPresenter(WorkshopManual _view, float _documentHeight, Vector3 _restingScale)
        {
            m_view = _view;
            m_documentHeight = _documentHeight;
            m_restingScale = _restingScale;
        }

        public bool Initialize()
        {
            if (m_isInitialized)
            {
                return true;
            }

            if (m_view == null)
            {
                Debug.LogError(
                    $"[{PRESENTER_NAME}] Initialize 실패: WorkshopManual 참조가 없습니다.");
                return false;
            }

            m_isInitialized = true;
            return true;
        }

        /// <summary>
        /// 읽는 카메라를 따라 종이의 목표 위치·회전·배율로 보간 적용합니다.
        /// </summary>
        public void Tick(Camera _camera, float _unscaledDeltaTime)
        {
            if (!CanRead() || _camera == null)
            {
                return;
            }

            Transform document = m_view.DocumentTransform;
            float distance = CalculateDistance(_camera);
            Vector3 targetPosition = _camera.transform.TransformPoint(new Vector3(m_pan.x, m_pan.y, distance));
            Quaternion targetRotation = _camera.transform.rotation * Quaternion.Euler(-90f, 0f, 0f);
            float blend = 1f - Mathf.Exp(-POSITION_BLEND_RATE * _unscaledDeltaTime);
            Vector3 scale = Vector3.Lerp(document.localScale, m_restingScale * m_zoom, blend);
            Vector3 position = Vector3.Lerp(document.position, targetPosition, blend);
            Quaternion rotation = Quaternion.Slerp(document.rotation, targetRotation, blend);
            m_view.ApplyReadingPose(position, rotation, scale);
        }

        /// <summary>
        /// 휠 줌과 드래그 팬을 기존 범위·한계로 갱신합니다.
        /// </summary>
        public void Adjust(Camera _camera, float _scroll, Vector2 _dragPixels)
        {
            if (!CanRead() || _camera == null)
            {
                return;
            }

            // Input System 휠 값은 틱 단위이거나 고해상도 델타일 수 있다.
            if (Mathf.Abs(_scroll) > .01f)
            {
                m_zoom = Mathf.Clamp(m_zoom * Mathf.Exp(Mathf.Sign(_scroll) * ZOOM_STEP), ZOOM_MIN, ZOOM_MAX);
            }

            float distance = CalculateDistance(_camera);
            float unitsPerPixel = 2f * distance * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f) /
                Mathf.Max(1, _camera.pixelHeight);
            m_pan += _dragPixels * unitsPerPixel;
            float limit = m_documentHeight * m_zoom * PAN_LIMIT_RATIO;
            m_pan.x = Mathf.Clamp(m_pan.x, -limit, limit);
            m_pan.y = Mathf.Clamp(m_pan.y, -limit, limit);
        }

        /// <summary>
        /// 줌·팬을 처음 값으로 되돌립니다.
        /// </summary>
        public void Reset()
        {
            m_zoom = 1f;
            m_pan = Vector2.zero;
        }

        public void Dispose()
        {
            Reset();
            m_isDisposed = true;
            m_isInitialized = false;
        }

        private bool CanRead()
        {
            return m_isInitialized && !m_isDisposed;
        }

        private float CalculateDistance(Camera _camera)
        {
            float distance = m_documentHeight / (FIT_SCALE * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f));
            distance *= Mathf.Max(1f, MIN_ASPECT / _camera.aspect);
            return Mathf.Max(distance, _camera.nearClipPlane + NEAR_CLIP_MARGIN);
        }
    }
}
