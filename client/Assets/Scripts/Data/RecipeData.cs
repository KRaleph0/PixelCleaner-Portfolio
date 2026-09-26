using System.Collections.Generic;
using PixelCleaners.Capture;

namespace PixelCleaners
{
    public enum CraftOutputType { Resource, CaptureTool }
    public enum RecipeCategory  { Synthesis, Craft }

    public class CraftIngredient
    {
        public ResourceType type;
        public int          amount;
        public CraftIngredient(ResourceType t, int a) { type = t; amount = a; }
    }

    public class Recipe
    {
        public string                name;
        public RecipeCategory        category;
        public List<CraftIngredient> ingredients;
        public CraftOutputType       outputType;
        public ResourceType          outputResource;   // outputType == Resource 일 때
        public CaptureToolTier       captureToolTier;  // outputType == CaptureTool 일 때
        public int                   outputCount       = 1;
        public float                 craftCycleSeconds = 60f;
    }

    public static class RecipeBook
    {
        // 합성 제작소용 레시피 (기초/1차 자원 → 2차 자원)
        public static readonly List<Recipe> Synthesis = new()
        {
            new Recipe {
                name              = "재생 복합재",
                category          = RecipeCategory.Synthesis,
                craftCycleSeconds = 60f,
                ingredients       = new() {
                    new(ResourceType.Garbage, 3),
                    new(ResourceType.Plastic, 2),
                    new(ResourceType.Paper,   2),
                },
                outputType     = CraftOutputType.Resource,
                outputResource = ResourceType.RecycledComposite,
                outputCount    = 1
            },
            new Recipe {
                name              = "재생 합금",
                category          = RecipeCategory.Synthesis,
                craftCycleSeconds = 90f,
                ingredients       = new() {
                    new(ResourceType.Garbage, 3),
                    new(ResourceType.Metal,   2),
                    new(ResourceType.Can,     2),
                },
                outputType     = CraftOutputType.Resource,
                outputResource = ResourceType.RecycledAlloy,
                outputCount    = 1
            },
        };

        // 고급 제작소용 레시피 (포획구 & 납품 아이템)
        public static readonly List<Recipe> Craft = new()
        {
            new Recipe {
                name              = "강화 포획구",
                category          = RecipeCategory.Craft,
                craftCycleSeconds = 45f,
                ingredients       = new() {
                    new(ResourceType.Metal,         2),
                    new(ResourceType.RecycledAlloy, 1),
                },
                outputType      = CraftOutputType.CaptureTool,
                captureToolTier = CaptureToolTier.Enhanced,
                outputCount     = 1
            },
            new Recipe {
                name              = "정밀 포획구",
                category          = RecipeCategory.Craft,
                craftCycleSeconds = 90f,
                ingredients       = new() {
                    new(ResourceType.Glass,             2),
                    new(ResourceType.RecycledComposite, 1),
                    new(ResourceType.RecycledAlloy,     1),
                },
                outputType      = CraftOutputType.CaptureTool,
                captureToolTier = CaptureToolTier.Precision,
                outputCount     = 1
            },
            new Recipe {
                name              = "픽셀 포획구",
                category          = RecipeCategory.Craft,
                craftCycleSeconds = 180f,
                ingredients       = new() {
                    new(ResourceType.PixelFragment,     3),
                    new(ResourceType.RecycledComposite, 2),
                    new(ResourceType.RecycledAlloy,     2),
                },
                outputType      = CraftOutputType.CaptureTool,
                captureToolTier = CaptureToolTier.Pixel,
                outputCount     = 1
            },
            new Recipe {
                name              = "납품 패키지",
                category          = RecipeCategory.Craft,
                craftCycleSeconds = 30f,
                ingredients       = new() {
                    new(ResourceType.Paper,   3),
                    new(ResourceType.Textile, 2),
                },
                outputType     = CraftOutputType.Resource,
                outputResource = ResourceType.DeliveryItem,
                outputCount    = 1
            },
            new Recipe {
                name              = "고급 납품 패키지",
                category          = RecipeCategory.Craft,
                craftCycleSeconds = 60f,
                ingredients       = new() {
                    new(ResourceType.Plastic,           3),
                    new(ResourceType.RecycledComposite, 1),
                },
                outputType     = CraftOutputType.Resource,
                outputResource = ResourceType.AdvancedDeliveryItem,   // 250pt (예전: 일반 납품 물품 ×2 = 200pt)
                outputCount    = 1
            },
        };

        // 팝업에서 카테고리별 필터용
        public static List<Recipe> ForCategory(RecipeCategory cat)
            => cat == RecipeCategory.Synthesis ? Synthesis : Craft;
    }
}
