namespace FuzzPhyte.Utility.Audio
{
    using UnityEngine;
    /// <summary>
    /// Uses the SpectrumData from an AudioSource to associate and enable a particle system
    /// to 'pulse' based on the audio
    /// </summary>
    public class FP_Audio_Pulse : MonoBehaviour
    {
        public AudioSource AudioPulse;
        [Tooltip("WHen we want to visualize our two tone speakers")]
        public bool SpeakerMode;
        public ParticleSystem ItemEffect;
        public ParticleSystem WooferItem;
       
        private ParticleSystem.EmissionModule WooferEmission;
       
        private ParticleSystem.MainModule MainModuleWoofer;
       
        public ParticleSystem.EmissionModule ItemEmission;
        public ParticleSystem.MainModule MainModule;

        public bool PulseOn;
        public int _band;
        public float _startScale, _scaleMultiplier, EmissionScale, SimSpeedScale;
        public static float[] _samples = new float[512];
        public static float[] _freqBand = new float[8];

        protected virtual void Start()
        {
            if(ItemEffect ==null || WooferItem == null)
            {
                Debug.LogError($"Missing Particle System References");
                return;
            }
            ItemEmission = ItemEffect.emission;
            MainModule = ItemEffect.main;

            if (SpeakerMode)
            {
                WooferEmission = WooferItem.emission;
                //TweeterEmission = TweeterItem.emission;
                MainModuleWoofer = WooferItem.main;
                //MainModuleTweeter = TweeterItem.main;
            }
        }

        /// <summary>
        /// use Unity Update to loop over Spectrum Data
        /// </summary>
        protected virtual void Update()
        {
            if (!PulseOn)
            {
                return;
            }
            AudioPulse.GetSpectrumData(_samples, 0, FFTWindow.Blackman);
            MakeFrequencyBands();
            var scaleSize = (_freqBand[_band] * _scaleMultiplier) + _startScale;
            if (SpeakerMode)
            {
                WooferEmission.rateOverTime = scaleSize * EmissionScale;
                MainModuleWoofer.simulationSpeed = Mathf.Clamp(scaleSize * SimSpeedScale, 0.1f, 1f);
                return;
            }
            ItemEmission.rateOverTime = scaleSize * EmissionScale;
            MainModule.simulationSpeed = Mathf.Clamp(scaleSize * SimSpeedScale, 1, 5);
            //ItemToPulse.localScale = new Vector3(scaleSize, scaleSize, scaleSize);
        }
        /// <summary>
        /// Activate the Pulse
        /// </summary>
        public virtual void ActivatePulse()
        {
            //ItemToPulse.localScale = new Vector3(_startScale, _startScale, _startScale);
            PulseOn = true;
            if (SpeakerMode)
            {
                WooferItem.Play();
                return;
            }
            ItemEffect.Play();
        }
        /// <summary>
        /// Deactivate the pulse
        /// </summary>
        public void DeactivatePulse()
        {
            PulseOn = false;
            if (SpeakerMode)
            {
                WooferItem.Stop();
                return;
            }
            //ItemToPulse.localScale = new Vector3(_startScale, _startScale, _startScale);
            ItemEffect.Stop();
        }

        protected void MakeFrequencyBands()
        {
            int count = 0;

            for (int i = 0; i < 8; i++)
            {
                float average = 0;
                int sampleCount = (int)Mathf.Pow(2, i) * 2;

                if (i == 7)
                {
                    sampleCount += 2;
                }
                for (int j = 0; j < sampleCount; j++)
                {
                    average += _samples[count] * (count + 1);
                    count++;
                }

                average /= count;

                _freqBand[i] = average * 10;
            }
        }
    }
}
