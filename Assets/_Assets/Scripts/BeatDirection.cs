using UnityEngine;

namespace _Assets.Scripts
{
    public class BeatDirection : MonoBehaviour
    {
        private bool _beatDetected = false;
        private void Awake()
        {
            AudioProcessor processor = FindObjectOfType<AudioProcessor>();
            processor.onBeat.AddListener(BeatDetected);
            processor.onSpectrum.AddListener(SpectrumChanged);
        }
        
        private void BeatDetected()
        {
            _beatDetected = true;
        }

        private void SpectrumChanged(float[] spectrum)
        {
            if (_beatDetected)
            {
                _beatDetected = false;
                for (int i = 0; i < spectrum.Length; i++)
                {
                    if (spectrum[0] > 0.5 || spectrum[1] > 0.5 || spectrum[2] > 0.5)
                    {
                        Debug.Log("left");
                        break;
                    }

                    if (spectrum[3] > 0.5 || spectrum[4] > 0.5 || spectrum[5] > 0.5)
                    {
                        Debug.Log("right");
                        break;
                    }
                    
                    if(spectrum[6] > 0.5 || spectrum[7] > 0.5 || spectrum[8] > 0.5)
                    {
                        Debug.Log("up");
                        break;
                    }
                    
                    if(spectrum[9] > 0.5 || spectrum[10] > 0.5 || spectrum[11] > 0.5)
                    {
                        Debug.Log("down");
                        break;
                    }
                    
                }   
            }
        }
    }
}