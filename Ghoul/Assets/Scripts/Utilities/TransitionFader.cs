using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionFader : ScreenFader
{
    [SerializeField] float _lifetime = 1f;
    [SerializeField] float _startDelay = 0f;
    [SerializeField] float _midFadeDelay = 0.1f;

    protected void Awake()
    {
        _lifetime = _startDelay + FadeOnDuration + _midFadeDelay + FadeOffDuration;
    }

    private IEnumerator PlayRoutine()
    {
        SetAlpha(_clearAlpha);
        yield return new WaitForSeconds(_startDelay);

        FadeOn();
        float onTime = FadeOnDuration + _midFadeDelay;
        yield return new WaitForSeconds(onTime);

        FadeOff();
        Destroy(gameObject, FadeOffDuration);
    }

    public void Play()
    {
        StartCoroutine(PlayRoutine());
    }

    public static float PlayTransition(TransitionFader transitionPrefab)
    {
        if (transitionPrefab != null)
        {
            TransitionFader instance = Instantiate(transitionPrefab, Vector3.zero, Quaternion.identity);
            instance.Play();
        }

        // add 0.1f (to ensure scene changes before fadeOff begins)
        float timeToSwitchScenes = transitionPrefab.FadeOnDuration + transitionPrefab._startDelay;
        return timeToSwitchScenes;
    }
}
