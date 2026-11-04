using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

public static class ClipRootInfo
{
    [MenuItem("Tools/Clip Root Info")]
    static void Run()
    {
        var clips = Selection.objects.OfType<AnimationClip>().ToArray();
        if (clips.Length == 0)
        {
            // FBX 자체를 선택한 경우 안의 클립을 전부 가져옴
            clips = Selection.objects
                .SelectMany(o => AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(o)))
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();
        }

        var sb = new StringBuilder();
        sb.AppendLine("name | len | loop | totalYaw | fwd | side | startSpd | endSpd | minSpd@t | meanSpd");

        foreach (var clip in clips.OrderBy(c => c.name))
            sb.AppendLine(Analyze(clip));

        Debug.Log(sb.ToString());
    }

    static string Analyze(AnimationClip clip)
    {
        var bindings = AnimationUtility.GetCurveBindings(clip);
        AnimationCurve C(string prop) => bindings
            .Where(b => b.propertyName == prop)
            .Select(b => AnimationUtility.GetEditorCurve(clip, b))
            .FirstOrDefault();

        var tx = C("RootT.x"); var tz = C("RootT.z");
        var qx = C("RootQ.x"); var qy = C("RootQ.y"); var qz = C("RootQ.z"); var qw = C("RootQ.w");
        if (tx == null || tz == null || qy == null)
            return $"{clip.name} | RootT/RootQ 커브 없음 (Humanoid 클립인지 확인)";

        int n = Mathf.Max(20, Mathf.RoundToInt(clip.length * 60f));
        float dt = clip.length / n;

        Quaternion Q(float t) => new Quaternion(qx.Evaluate(t), qy.Evaluate(t), qz.Evaluate(t), qw.Evaluate(t)).normalized;
        Vector3 P(float t) => new Vector3(tx.Evaluate(t), 0f, tz.Evaluate(t));
        float Yaw(float t) => Q(t).eulerAngles.y;

        // 총 회전 (언랩해서 누적)
        float totalYaw = 0f, prevYaw = Yaw(0f);
        for (int i = 1; i <= n; i++)
        {
            float y = Yaw(i * dt);
            totalYaw += Mathf.DeltaAngle(prevYaw, y);
            prevYaw = y;
        }

        // 시작 방향 기준 로컬 변위
        Quaternion startInv = Quaternion.Euler(0f, Yaw(0f), 0f);
        startInv = Quaternion.Inverse(startInv);
        Vector3 disp = startInv * (P(clip.length) - P(0f));

        // 속도 샘플
        float[] spd = new float[n];
        for (int i = 0; i < n; i++)
            spd[i] = (P((i + 1) * dt) - P(i * dt)).magnitude / dt;

        int edge = Mathf.Max(1, Mathf.RoundToInt(0.1f / dt));
        float startSpd = spd.Take(edge).Average();
        float endSpd = spd.Skip(n - edge).Average();
        int minI = System.Array.IndexOf(spd, spd.Min());
        float mean = spd.Average();

        return $"{clip.name} | {clip.length:F3} | {clip.isLooping} | {totalYaw:F1} | {disp.z:F2} | {disp.x:F2} | " +
               $"{startSpd:F2} | {endSpd:F2} | {spd[minI]:F2}@{minI * dt:F2}s | {mean:F2}";
    }
}