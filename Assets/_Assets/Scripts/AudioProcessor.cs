using System;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

[RequireComponent(typeof(AudioSource))]
public class AudioProcessor : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private int fftBufferSize = 1024;
    [SerializeField] private int samplingRate = 44100;
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Events")]
    public OnBeatEventHandler onBeat;
    public OnSpectrumEventHandler onSpectrum;

    private long _lastTimeMilliseconds, _currentTimeMilliseconds;
    private long _tapTempoDelta, _tapTempoEntries, _tapTempoSum;
    private const int NumberOfBands = 12;
    private int _sinceLastBeat;

    // counter to suppress double-beats
    private float _framePeriod;

    private const int ColumnMax = 120;
    private float[] _spectrum;
    private float[] _averages;
    private float[] _onsets;
    private float[] _scores;
    
    private int _currentColumnIndex;
    private float[] _spectrumOfPreviousStep;
    private const int MaxFramesLagToTrack = 100;
    private const float Decay = 0.997f;
    private AutoCorrelator _autoCorrelator;

    private void Awake()
    {
        InitArrays();

        audioSource = GetComponent<AudioSource>();
        samplingRate = audioSource.clip.frequency;

        _framePeriod = (float)fftBufferSize / (float)samplingRate;

        //initialize record of previous spectrum
        _spectrumOfPreviousStep = new float[NumberOfBands];
        for (var i = 0; i < NumberOfBands; ++i)
        {
            _spectrumOfPreviousStep[i] = 100.0f;
        }

        _autoCorrelator = new AutoCorrelator(MaxFramesLagToTrack, Decay, _framePeriod, GetBandWidth());

        _lastTimeMilliseconds = GetCurrentTimeMilliseconds();
    }

    private void Start()
    {
        audioSource.Play();
    }

    private void InitArrays()
    {
        _onsets = new float[ColumnMax];
        _scores = new float[ColumnMax];
        _spectrum = new float[fftBufferSize];
        _averages = new float[12];
    }

    private static long GetCurrentTimeMilliseconds()
    {
        long milliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
        return milliseconds;
    }

    private void Update()
    {
        if (audioSource.isPlaying)
        {
            audioSource.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
            ComputeAverages(_spectrum);
            onSpectrum.Invoke(_averages);

            float onset = CalculateOnset();
            _onsets[_currentColumnIndex] = onset;

            int tempo = UpdateAutoCorrelatorAndGetTempo();
            UpdateScoreFunction(tempo);
            CheckForBeat(tempo);
        }
    }

    private float CalculateOnset()
    {
        float onset = 0;
        for (int i = 0; i < NumberOfBands; i++)
        {
            float specVal = CalculateSpectrumValue(i);
            float dbInc = specVal - _spectrumOfPreviousStep[i];
            _spectrumOfPreviousStep[i] = specVal;
            onset += dbInc;
        }

        return onset;
    }

    private float CalculateSpectrumValue(int bandIndex)
    {
        float spectrumValue = Mathf.Max(-100.0f, 20.0f * Mathf.Log10(_averages[bandIndex]) + 160);
        spectrumValue *= 0.025f;
        return spectrumValue;
    }

    private int UpdateAutoCorrelatorAndGetTempo()
    {
        _autoCorrelator.NewValue(_onsets[_currentColumnIndex]);
        float max = 0.0f;
        int tempo = 0;

        for (int i = 0; i < MaxFramesLagToTrack; ++i)
        {
            float current = Mathf.Sqrt(_autoCorrelator.CurrentAutoCorrelator(i));
            if (current > max)
            {
                max = current;
                tempo = i;
            }
        }

        return tempo;
    }

    private void UpdateScoreFunction(int tempo)
    {
        var scoreMax = -999999f;

        for (int i = tempo / 2; i < Mathf.Min(ColumnMax, 2 * tempo); ++i)
        {
            float score = CalculateScore(tempo, i);
            if (score > scoreMax)
            {
                scoreMax = score;
            }
        }

        _scores[_currentColumnIndex] = scoreMax;

        float scoreMin = _scores[0];
        for (int i = 0; i < ColumnMax; ++i)
        {
            if (_scores[i] < scoreMin)
            {
                scoreMin = _scores[i];
            }
        }

        for (int i = 0; i < ColumnMax; ++i)
        {
            _scores[i] -= scoreMin;
        }
    }

    private float CalculateScore(int tempo, int index)
    {
        float tempoPenaltyFactor = 100 * sensitivity;
        float score = _onsets[_currentColumnIndex] + _scores[(_currentColumnIndex - index + ColumnMax) % ColumnMax] -
                      tempoPenaltyFactor * Mathf.Pow(Mathf.Log((float)index / (float)tempo), 2);
        return score;
    }

    //TESTED
    private void CheckForBeat(int tempo)
    {
        var scoreMax = -999999f;
        var scoreMaxIndex = 0;
        for (int i = 0; i < ColumnMax; ++i)
        {
            if (_scores[i] > scoreMax)
            {
                scoreMax = _scores[i];
                scoreMaxIndex = i;
            }
        }
        
        ++_sinceLastBeat;
        if (scoreMaxIndex == _currentColumnIndex)
        {
            if (_sinceLastBeat > tempo / 4)
            {
                onBeat.Invoke();
                _sinceLastBeat = 0;
            }
        }

        if (++_currentColumnIndex == ColumnMax)
        {
            _currentColumnIndex = 0;
        }
    }

    private float GetBandWidth() => 2f / fftBufferSize * (samplingRate / 2f);

    private int FrequencyToIndex(int frequency)
    {
        // special case: freq is lower than the bandwidth of spectrum[0]
        if (frequency < GetBandWidth() / 2)
        {
            return 0;
        }

        // special case: freq is within the bandwidth of spectrum[512]
        if (frequency > samplingRate / 2f - GetBandWidth() / 2)
        {
            return fftBufferSize / 2;
        }

        float fraction = (float)frequency / (float)samplingRate;
        int index = Mathf.RoundToInt(fftBufferSize * fraction);
        return index;
    }

    private void ComputeAverages(float[] spectrum)
    {
        for (int i = 0; i < 12; i++)
        {
            float avg = 0;

            var lowestFrequency = i == 0 ? 0 : Mathf.FloorToInt(samplingRate / 2f / Mathf.Pow(2, 12 - i));

            var highestFrequency = Mathf.FloorToInt(samplingRate / 2f / Mathf.Pow(2, 11 - i));
            var lowestBound = FrequencyToIndex(lowestFrequency);
            var highestBound = FrequencyToIndex(highestFrequency);

            for (var bound = lowestBound; bound <= highestBound; bound++)
            {
                avg += spectrum[bound];
            }

            // line has been changed since discussion in the comments
            // avg /= (hiBound - lowBound);
            avg /= highestBound - lowestBound + 1;
            _averages[i] = avg;
        }
    }

    private void TapTempo()
    {
        _currentTimeMilliseconds = GetCurrentTimeMilliseconds();
        _tapTempoDelta = _currentTimeMilliseconds - _lastTimeMilliseconds;
        _lastTimeMilliseconds = _currentTimeMilliseconds;
        _tapTempoSum += _tapTempoDelta;
        _tapTempoEntries++;

        var average = (int)(_tapTempoSum / _tapTempoEntries);

        Debug.Log($"average = {average}");
    }

    private double[] ToDoubleArray(float[] arr)
    {
        if (arr == null)
            return null;
        int n = arr.Length;
        double[] doubleArray = new double[n];
        for (int i = 0; i < n; i++)
        {
            doubleArray[i] = arr[i];
        }

        return doubleArray;
    }

    private void ChangeCameraColor()
    {
        var red = Random.Range(0f, 1f);
        var green = Random.Range(0f, 1f);
        var blue = Random.Range(0f, 1f);

        var color = new Color(red, green, blue);

        Camera.main.clearFlags = CameraClearFlags.Color;
        Camera.main.backgroundColor = color;

        Camera.main.backgroundColor = color;
    }

    [Serializable]
    public class OnBeatEventHandler : UnityEvent
    {
    }

    [Serializable]
    public class OnSpectrumEventHandler : UnityEvent<float[]>
    {
    }

    private class AutoCorrelator
    {
        private readonly int _delayLength;
        private readonly float _decay;
        private readonly float[] _delays;
        private readonly float[] _outputs;
        private int _index;

        private readonly float[] _BPMs;
        private readonly float[] _rweight;
        private const float Wmidbpm = 120f;

        public AutoCorrelator(int decayLength, float decay, float framePeriod, float bandwidth)
        {
            _decay = decay;
            _delayLength = decayLength;
            _delays = new float[_delayLength];
            _outputs = new float[_delayLength];
            _index = 0;

            // calculate a log-lag gaussian weighting function, to prefer tempo around 120 bpm
            _BPMs = new float[_delayLength];
            _rweight = new float[_delayLength];
            for (int i = 0; i < _delayLength; ++i)
            {
                _BPMs[i] = 60.0f / (framePeriod * i);
                // weighting is Gaussian on log-BPM axis, centered at wmidbpm, SD = woctavewidth octaves
                _rweight[i] =
                    Mathf.Exp(-0.5f * Mathf.Pow(Mathf.Log(_BPMs[i] / Wmidbpm) / Mathf.Log(2.0f) / bandwidth, 2.0f));
            }
        }

        public void NewValue(float val)
        {
            _delays[_index] = val;

            for (var i = 0; i < _delayLength; ++i)
            {
                var delayIndex = (_index - i + _delayLength) % _delayLength;
                _outputs[i] += (1 - _decay) * (_delays[_index] * _delays[delayIndex] - _outputs[i]);
            }

            if (++_index == _delayLength)
            {
                _index = 0;
            }
        }

        public float CurrentAutoCorrelator(int del) => _rweight[del] * _outputs[del];

        public float AverageBPM()
        {
            float sum = 0;

            for (int i = 0; i < _BPMs.Length; ++i)
            {
                sum += _BPMs[i];
            }

            return sum / _delayLength;
        }
    }
}