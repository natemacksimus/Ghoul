using UnityEngine;

public static class Helpers
{
    public static float Map(float value, float originalMin, float originalMax, float newMin, float newMax, bool clamp)
    {
        float newValue = (value - originalMin) / (originalMax - originalMin) * (newMax - newMin) + newMin;
        if (clamp)
        {
            newValue = Mathf.Clamp(newValue, newMin, newMax);
        }
        return newValue;
    }


    // Rounds a float to the nearest half value : 1, 1.5, 2, 2.5 etc
    public static float RoundToNearestHalf(float a)
    {
        return a = a - (a % 0.5f);
    }


    // Returns a random Vector2 from 2 defined Vector2.
    public static Vector2 RandomVector2(Vector2 minimum, Vector2 maximum)
    {
        return new Vector2(UnityEngine.Random.Range(minimum.x, maximum.x),
            UnityEngine.Random.Range(minimum.y, maximum.y));
    }

    public static float RoundToDecimal(float value, int numberOfDecimals)
    {
        if (numberOfDecimals <= 0)
        {
            return Mathf.Round(value);
        }
        else
        {
            return Mathf.Round(value * 10f * numberOfDecimals) / (10f * numberOfDecimals);
        }
    }

    public static float GetAnimationLength(Animator animator, string animationName)
    {

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == animationName) { return clip.length; }
        }

        return 0;
    }
}
