// Copyright (c) 2026 John B. Shull
// FuzzPhyte LLC is a company associated with John B. Shull
// This file is part of FP_Utility Package.
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md COMMERCIAL-LICENSE.md, and NOTICE.md.

namespace FuzzPhyte.Utility.LabelDisplay
{
    using FuzzPhyte.Utility;
    using System.Collections;
    using System.Collections.Generic;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class Label_Box_Controller : MonoBehaviour
    {
        [Header("Main References")]
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] Image headPicture;
        [SerializeField] Image secondaryPicture;
        [SerializeField] Image secondaryBorder;

        [Header("Color Components")]
        [SerializeField] Image mainColorComponent;
        [SerializeField] Image secondaryColorComponent;
        [SerializeField] Image tertiaryColorComponent;

        [Header("Progress Bar")]
        [SerializeField] Transform progressBarTransform;
        private Coroutine progressBarCoroutine;

        [Header("Misc")]
        [SerializeField] RemoteImageLoader imageLoader;

        FP_Theme theme;

        float waitTime;
        int messagesDisplayed;

        List<LabelLine> fullLabel = new List<LabelLine>();

        public void Init(FP_Theme themeInput, List<LabelLine> messages, float waitTimeInput)
        {
            Debug.Log("Ran");

            theme = themeInput;
            waitTime = waitTimeInput;

            SetProgressBar(progressBarTransform);

            mainColorComponent.color = themeInput.MainColor;
            secondaryColorComponent.color = themeInput.SecondaryColor;
            tertiaryColorComponent.color = themeInput.TertiaryColor;

            headPicture.sprite = theme.Icon;

            fullLabel.Clear();
            fullLabel.AddRange(messages);

            messagesDisplayed = 0;

            if (fullLabel.Count > 0)
            {
                StartCoroutine(DisplayMessage(fullLabel[messagesDisplayed]));
                StartCoroutine(UpdateProgressBar());

                messagesDisplayed++;

                StartCoroutine(UpdateText());
            }

            VRDirectionIndicator indicator = FindAnyObjectByType<VRDirectionIndicator>();
            if (indicator != null)
                indicator.actionTarget = transform;
        }

        private FontSetting FindFontSetting(FontSettingLabel label)
        {
            foreach (FontSetting setting in theme.FontSettings)
            {
                if (setting.Label == label)
                {
                    return setting;
                }
            }

            return null;
        }

        private void ApplyFontSetting(TextMeshProUGUI textObject, FontSetting fontSetting)
        {
            if (fontSetting == null)
            {
                return;
            }

            // Font, Color, Alignment, Size
            textObject.font = fontSetting.Font;
            textObject.color = fontSetting.FontColor;
            textObject.fontStyle = fontSetting.FontStyle;
            textObject.alignment = fontSetting.FontAlignment;
            if (fontSetting.UseAutoSizing)
            {
                textObject.enableAutoSizing = true;

                textObject.fontSizeMin = fontSetting.MinSize;
                textObject.fontSizeMax = fontSetting.MaxSize;
            }
            else
            {
                textObject.enableAutoSizing = false;
                textObject.fontSize = fontSetting.MaxSize;
            }
        }

        private IEnumerator DisplayMessage(LabelLine line)
        {
            if (!string.IsNullOrEmpty(line.headImage))
            {
                Debug.Log("Attempting to load image...");

                yield return StartCoroutine(imageLoader.LoadImage(line.headImage, sprite =>
                {
                    Debug.Log($"Image callback received. Sprite: {sprite}");

                    if (sprite != null)
                    {
                        headPicture.sprite = sprite;
                    }
                }));
            }
            else
            {
                Debug.Log("No head image specified.");
            }

            if (!string.IsNullOrEmpty(line.secondImage))
            {
                bool finished = false;
                Sprite loadedSprite = null;

                Color alphaColor = secondaryPicture.color;
                alphaColor.a = 1;
                secondaryPicture.color = alphaColor;
                secondaryBorder.color = alphaColor;

                StartCoroutine(imageLoader.LoadImage(line.secondImage, sprite =>
                        {
                            loadedSprite = sprite;
                            finished = true;
                        }
                    ));

                while (!finished)
                {
                    yield return null;
                }

                if (loadedSprite != null)
                {
                    secondaryPicture.sprite = loadedSprite;
                }
            }
            else
            {
                Color alphaColor = secondaryPicture.color;
                alphaColor.a = 0;
                secondaryPicture.color = alphaColor;
                secondaryBorder.color = alphaColor;
            }

            FontSetting speakerFont = FindFontSetting(line.speakerLabel);
            FontSetting messageFont = FindFontSetting(line.messageLabel);

            if (speakerFont != null)
            {
                ApplyFontSetting(nameText, speakerFont);
            }

            if (messageFont != null)
            {
                ApplyFontSetting(bodyText, messageFont);
            }

            if (!string.IsNullOrEmpty(line.speaker))
            {
                nameText.text = line.speaker;
            }

            bodyText.text = line.message;

            Debug.Log($"Image ID: '{line.headImage}'");
        }

        private IEnumerator UpdateText()
        {
            yield return new WaitForSeconds(waitTime);

            if (messagesDisplayed >= fullLabel.Count)
            {
                Destroy(gameObject);
            }
            else
            {
                StartCoroutine(DisplayMessage(fullLabel[messagesDisplayed]));
                StartCoroutine(UpdateProgressBar());

                messagesDisplayed++;

                StartCoroutine(UpdateText());
            }
        }

        public void SetProgressBar(Transform progressBarObject)
        {
            progressBarTransform = progressBarObject;

            if (progressBarTransform != null)
            {
                Vector3 scale = progressBarTransform.localScale;
                scale.x = 0f;
                progressBarTransform.localScale = scale;
            }
        }

        private void StartProgressBar()
        {
            if (progressBarCoroutine != null)
                StopCoroutine(progressBarCoroutine);

            progressBarCoroutine = StartCoroutine(UpdateProgressBar());
        }

        private IEnumerator UpdateProgressBar()
        {
            if (progressBarTransform == null)
                yield break;

            Vector3 scale = progressBarTransform.localScale;
            scale.x = 0f;
            progressBarTransform.localScale = scale;

            float elapsed = 0f;

            while (elapsed < waitTime)
            {
                elapsed += Time.deltaTime;
                scale.x = Mathf.Clamp01(elapsed / waitTime);
                progressBarTransform.localScale = scale;

                yield return null;
            }

            scale.x = 1f;
            progressBarTransform.localScale = scale;
        }
    }
}
