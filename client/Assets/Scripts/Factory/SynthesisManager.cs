using System.Collections.Generic;
using UnityEngine;

namespace PixelCleaners
{
    public class SynthesisManager : MonoBehaviour
    {
        public static SynthesisManager Instance { get; private set; }

        public const int SlotCount      = 4;
        public const int SynthesisCount = 2; // 슬롯 0-1: 일반 제작소 (2차 자원) — 재구성력
        public const int CraftCount     = 2; // 슬롯 2-3: 고급 제작소 (납품 아이템 + 포획구) — 합성력

        readonly SynthesisSlot[] slots = new SynthesisSlot[SlotCount];

        public SynthesisSlot               GetSlot(int i) => slots[i];
        public IReadOnlyList<SynthesisSlot> Slots         => slots;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = new SynthesisSlot(i);
                slots[i].RequiresCreature = true; // 일반 제작소 + 고급 제작소 모두 크리처 필요
            }
        }

        void Update()
        {
            foreach (var s in slots)
                s.Tick(Time.deltaTime);
        }
    }
}
