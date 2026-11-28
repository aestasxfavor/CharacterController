#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// "Slide into place" for UI Toolkit lists: when an element's layout position changes
    /// (e.g. a sibling was reordered), it starts at its old spot and glides to the new one.
    /// Costs nothing unless the layout actually moves while <c>enabled</c> returns true.
    /// </summary>
    static class EHSlide
    {
        static readonly StyleList<StylePropertyName> Prop =
            new StyleList<StylePropertyName>(new List<StylePropertyName> { new StylePropertyName("translate") });
        static readonly StyleList<TimeValue> Instant =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(0f, TimeUnit.Millisecond) });
        static readonly StyleList<TimeValue> Glide =
            new StyleList<TimeValue>(new List<TimeValue> { new TimeValue(170f, TimeUnit.Millisecond) });
        static readonly StyleList<EasingFunction> Ease =
            new StyleList<EasingFunction>(new List<EasingFunction> { new EasingFunction(EasingMode.EaseOutCubic) });

        public static void Attach(VisualElement ve, Func<bool> enabled)
        {
            ve.style.transitionProperty = Prop;
            ve.style.transitionTimingFunction = Ease;
            ve.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (!enabled()) return;
                var o = e.oldRect;
                var n = e.newRect;
                if (o.width <= 0f || n.width <= 0f) return;
                Vector2 d = o.position - n.position;
                if (d.sqrMagnitude < 0.25f) return;
                Vector3 cur = ve.resolvedStyle.translate; // continue from where an earlier slide was
                d.x += cur.x;
                d.y += cur.y;
                ve.style.transitionDuration = Instant;
                ve.style.translate = new Translate(d.x, d.y);
                ve.schedule.Execute(() =>
                {
                    ve.style.transitionDuration = Glide;
                    ve.style.translate = new Translate(0f, 0f);
                }).StartingIn(16);
            });
        }
    }
}
#endif
