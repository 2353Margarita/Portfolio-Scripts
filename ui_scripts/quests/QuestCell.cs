using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.UI;
using Yrr.Utils;
using System.Text;

namespace Game
{
    public sealed class QuestCell : MonoBehaviour
    {
        [SerializeField] private Image questImage;
        [SerializeField] private TextMeshProUGUI questNameTmp;
        [SerializeField] private Slider slider;
        [SerializeField] public GameObject visualRoot;

        [SerializeField] public ZooCustomButton questButton;
        [SerializeField] private TextMeshProUGUI questRewardTmp;
        [SerializeField] private GameObject workerRewardImage;

        [HideInInspector] public Quest_Base quest;


        public void Init(Quest_Base _quest, QuestsScreen _screen)
        {
            quest = _quest;
            questImage.sprite = quest.icon;
            questNameTmp.text = quest.uiName;

            string Rewards = "";

            for (int i = 0; i < quest.QuestRewards.CurrencyRewards.Count; i++)
            {
                CurrencyCost CC = quest.QuestRewards.CurrencyRewards[i];
                CurrencySettings CS = GameManager.Instance.GetCurrency(CC.CurrencyType);
                if (i > 0) Rewards += " ";
                Rewards += BuildCurrencyString(CS, CC);
            }
            Rewards += "\nReward";

            questRewardTmp.text = Rewards;
            questButton.OnClick.AddListener(() =>
            {
                SoundsManager.CreateSoundOnCamera("ui:button_taken_reward", 1, true);

                quest.ApplyReward(questButton.transform);
                quest = null;
                visualRoot.SetActive(false);
                _screen.UpdateData();
                _screen.StartCoroutine(Collapse(_screen));
                VibrationPaterns.StartVibration("quest_reward");
            });
            UpdateUI();
        }

        private string BuildCurrencyString(CurrencySettings CS, CurrencyCost CC)
        {
            var sb = new StringBuilder();
            sb.Append("<size=80%>");
            sb.Append(CS.textToken);
            sb.Append("</size> ");
            sb.Append(CC.Cost.ToShortMoneyString());

            return sb.ToString();
        }

        public void UpdateUI()
        {
            if (quest == null)
            {
                questButton.gameObject.SetActive(false);
                slider.value = 1.0f;
                return;
            }

            questButton.gameObject.SetActive(!quest.RewardIsReceived());
            questButton.interactable = quest.IsCompleted();
            if (quest.NeedUpdateName()) questNameTmp.text = quest.uiName;
            slider.value = quest.ProgressPercent();
        }

        IEnumerator Collapse(QuestsScreen screen)
        {
            RectTransform myRT = transform as RectTransform;
            float targetHeight = -screen.LayoutSpacing;
            float startHeight = myRT.rect.height;
            float timer = 0.0f;
            float max_time = screen.cellCollapseTimeSec;

            quest = null;
            visualRoot.SetActive(false);

            while (timer < max_time)
            {
                yield return new WaitForEndOfFrame();
                timer += Time.deltaTime;
                myRT.sizeDelta = new Vector2(myRT.sizeDelta.x, Mathf.Lerp(startHeight, targetHeight, timer / max_time));
                screen.UpdateLayout();
            }
        }
    }
}
