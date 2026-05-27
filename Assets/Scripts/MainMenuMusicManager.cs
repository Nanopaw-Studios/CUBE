using Unity.Mathematics;
using UnityEngine;

public class MainMenuMusicManager : MonoBehaviour
{
    public AudioSource source;
    public AnimationClip splash;
    public AudioLowPassFilter lowPass;

    public AudioClip intro;
    public AudioClip loop;

    bool isTransitioning = false;

    public void Start()
    {
        lowPass = source.GetComponent<AudioLowPassFilter>();

        source.clip = intro;
        source.time = Mathf.Max(0, intro.length - splash.length);

        lowPass.enabled = true;
        lowPass.cutoffFrequency = 1000;

        source.loop = false;
        source.Play();
        isTransitioning = true;
    }

    void Update()
    {
        if (isTransitioning && source.clip == intro)
        {
            float remainingTime = intro.length - source.time;

            if (remainingTime <= 1.0f)
            {
                lowPass.cutoffFrequency = Mathf.MoveTowards(
                    lowPass.cutoffFrequency,
                    22000f,
                    21000f * Time.deltaTime
                );
            }
        }

        if (isTransitioning && !source.isPlaying)
        {
            source.clip = loop;
            source.loop = true;
            source.Play();
            isTransitioning = false;
            lowPass.cutoffFrequency = 22000f;
        }
    }
}
