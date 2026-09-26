using UnityEngine;
using UnityEngine.InputSystem;
using PixelCleaners;
using PixelCleaners.Capture;
using PixelCleaners.UI;

namespace PixelCleaners.AR
{
    [RequireComponent(typeof(Collider))]
    public class CaptureInteraction : MonoBehaviour
    {
        CreatureInstance creature;
        bool captured;            // 미니게임 진행 중 (중복 탭 방지)
        float retryAllowedAt;     // "놓쳤다" 후 결과 화면을 닫는 탭이 곧바로 재도전으로 이어지지 않도록
        string mapSpawnKey;       // 지도에서 탭해 온 경우의 스폰 키
        long   mapSpawnEndsTicks;

        const float RetryCooldownSeconds = 0.4f;

        public void Initialize(CreatureInstance instance, string spawnKey = null, long spawnEndsTicks = 0)
        {
            creature          = instance;
            mapSpawnKey       = spawnKey;
            mapSpawnEndsTicks = spawnEndsTicks;
        }

        void Update()
        {
            if (captured) return;
            if (Time.unscaledTime < retryAllowedAt) return;
            if (Camera.main == null) return; // InitAR() 완료 전 터치 방어

            // 에디터: 마우스 클릭
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 15f) && hit.transform == transform)
                    OnTapped();
                return;
            }

            // 모바일: 터치
            // EventSystem.IsPointerOverGameObject(touchId)는 새 Input System에서
            // 신뢰할 수 없어 제거 — UI와 Physics 레이캐스트는 별개로 동작함
            var ts = Touchscreen.current;
            if (ts == null) return;
            foreach (var touch in ts.touches)
            {
                if (!touch.phase.ReadValue().Equals(UnityEngine.InputSystem.TouchPhase.Began))
                    continue;

                var touchRay = Camera.main.ScreenPointToRay(touch.position.ReadValue());
                if (Physics.Raycast(touchRay, out var touchHit, 15f) &&
                    touchHit.transform == transform)
                {
                    OnTapped();
                }
                break; // 첫 번째 터치만 처리
            }
        }

        void OnTapped()
        {
            captured = true;
            CreatureSpawner.Instance?.RemoveCreature(gameObject);

            // 하단 ToolSelectorUI에서 선택된 도구로 바로 미니게임 진입
            var selectedTool = ToolSelectorUI.SelectedTool;
            CaptureMiniGame.Launch(creature, selectedTool, outcome =>
            {
                switch (outcome)
                {
                    case CaptureOutcome.Success:
                        // 정화/제거 선택과 무관하게 이 지도 스폰은 잡은 것으로 기록
                        MapSpawnSystem.MarkCaught(mapSpawnKey, mapSpawnEndsTicks);
                        CaptureUI.Instance?.ShowCaptureChoice(creature, OnPurify, OnDiscard);
                        break;

                    case CaptureOutcome.Escaped:
                        // "놓쳤다!" — 도망가지 않았으므로 그 자리에서 다시 도전 (포획틀은 다시 소모)
                        captured       = false;
                        retryAllowedAt = Time.unscaledTime + RetryCooldownSeconds;
                        CreatureSpawner.Instance?.TrackCreature(gameObject);
                        break;

                    case CaptureOutcome.Fled:
                        // "도망쳤다!" — 지도의 해당 스폰을 지우고 지도로 강제 이동
                        MapSpawnSystem.MarkFled(mapSpawnKey, mapSpawnEndsTicks);
                        Destroy(gameObject);
                        SceneController.GoMap();
                        break;
                }
            });
        }

        void OnPurify()
        {
            creature.isPurified = true;
            if (FactoryManager.Instance != null) FactoryManager.Instance.AddCreatureToInventory(creature);
            Destroy(gameObject);
        }

        void OnDiscard()
        {
            if (FactoryManager.Instance != null) FactoryManager.Instance.AddAwakenItem(AwakenItemTier.Normal, 1);
            Destroy(gameObject);
        }
    }
}
