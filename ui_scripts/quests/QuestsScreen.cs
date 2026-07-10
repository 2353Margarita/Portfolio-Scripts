using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yrr.UI;
using Yrr.Utils;
using Yrr.UI.Elements;
using System;
using NSubstitute;
using System.Linq;

namespace Game
{
    public class QuestsScreen : UIScreen
    {
        [SerializeField] private RectTransform cellsRoot;
        [SerializeField] private QuestCell cellPrefab;
        [SerializeField] private Scrollbar scrollbar;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private VerticalLayoutGroup layout;
        [SerializeField] ContentSizeFitter contentSizeFitter;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform viewportUp;
        [SerializeField] private RectTransform viewportBottom;
        public float cellCollapseTimeSec = 1.5f;

        [SerializeField] private Slider slider;
        [SerializeField] private TextMeshProUGUI leftTmp;
        [SerializeField] private TextMeshProUGUI rightTmp;

        [SerializeField] private SmoothSlider allQuestsSlider;
        public Button closeButton;

        [SerializeField] private GameObject allQuestsIsCompletedLabelGo;


        public float LayoutSpacing
        {
            get
            {
                if (layout != null) return layout.spacing;
                return 0.0f;
            }
        }

        List<QuestCell> cells = new List<QuestCell>();


        private void Start()
        {
            scrollRect.onValueChanged.AddListener(UpdateVisibilityCells);
            QuestManager.Instance.OnAnyQuestRewardTaked += UpdateAllQuestSlider;
        }

        private void OnDestroy()
        {
            QuestManager.Instance.OnAnyQuestRewardTaked -= UpdateAllQuestSlider;
        }

        public void GenerateCells()
        {
            QuestManager.Instance.StartCoroutine(Generation());
        }

        IEnumerator Generation()
        {
            cellsRoot.ClearChildren();
            cells.Clear();
            int i = 0;

            yield return new WaitForEndOfFrame();

            foreach (var quest in QuestManager.Instance.quests)
            {
                if (quest.RewardIsReceived()) continue;

                i++;
                if (i > 10)
                {
                    i = 0;
                    yield return new WaitForEndOfFrame();
                }

                var clone = Instantiate(cellPrefab, cellsRoot);
                clone.Init(quest, this);
                cells.Add(clone);
            }
        }

        protected override void OnShow(object args)
        {
            SoundsManager.CreateSoundOnCamera("ui:show_screen", 2, true);

            //need this after changing amount of cells
            scrollbar.size = 0.1f;
            var point = cellsRoot.localPosition;
            point.y = 0.0f;
            cellsRoot.localPosition = point;

            CheckNullCells();
            UpdateData();
            SortCells();
            UpdateAllQuestSlider();
            Vizorlytic.OpenMenu("quests_screen");
        }

        protected override void OnHide()
        {
            base.OnHide();
            SoundsManager.CreateSoundOnCamera("ui:show_screen", 2, true);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public void UpdateLayout()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layout.transform as RectTransform);
        }

        public void UpdateData()
        {
            foreach (var cell in cells)
                cell.UpdateUI();
            UpdateAllQuestSlider();
        }

        void SortCells()
        {
            bool is_sorted = true;
            bool first_completed = false;

            if (cells.Count > 0) first_completed = cells[0].quest.IsCompleted();

            foreach (var cell in cells)
            {
                if (cell.quest.IsCompleted())
                {
                    if (!first_completed)
                    {
                        is_sorted = false;
                        break;
                    }
                }
                else
                {
                    if (first_completed)
                    {
                        is_sorted = false;
                        break;
                    }
                }
            }

            if (!is_sorted)
            {
                List<QuestCell> sort_cells = new List<QuestCell>();
                foreach (var cell in cells)
                {
                    if (cell.quest.IsCompleted()) sort_cells.Add(cell);
                }
                foreach (var cell in cells)
                {
                    if (!cell.quest.IsCompleted()) sort_cells.Add(cell);
                }
                foreach (var cell in sort_cells)
                {
                    cell.transform.SetAsLastSibling();
                }
                cells = sort_cells;
            }
            UpdateLayout();
            UpdateVisibilityCells(Vector3.zero);
        }

        IEnumerator UpdateVisibilityInNextFrame()
        {
            yield return new WaitForEndOfFrame();
            UpdateVisibilityCells(Vector3.zero);
        }

        void UpdateVisibilityCells(Vector2 value)
        {
            foreach (var cell in cells)
            {
                if (cell == null || cell.quest == null) continue;
                if (RectTransformInScrollViewport(cell.transform as RectTransform, true))
                    cell.visualRoot.gameObject.SetActive(true);
                else cell.visualRoot.gameObject.SetActive(false);
            }
        }

        void CheckNullCells(bool need_destroy = true)
        {
            List<QuestCell> del_cells = new List<QuestCell>();
            foreach (var cell in cells)
                if (cell == null || cell.quest == null)
                {
                    del_cells.Add(cell);
                }
            foreach (var del in del_cells)
            {
                cells.Remove(del);
                if (need_destroy && del != null) Destroy(del.gameObject);
            }
        }

        public RectTransform Tutorial_GetActiveRewardButton(bool alignment)
        {
            CheckNullCells(false);
            foreach (var cell in cells)
            {
                if (cell.quest.IsCompleted() && !cell.quest.RewardIsReceived())
                {
                    if (alignment)
                    {
                        var point = cellsRoot.localPosition;
                        point.y = -(cell.transform as RectTransform).localPosition.y - (cell.transform as RectTransform).sizeDelta.y * 0.5f;
                        cellsRoot.localPosition = point;
                    }
                    return cell.questButton.transform as RectTransform;
                }
            }
            return null;
        }

        public QuestCell Tutorial_GetActiveRewardCell(bool alignment)
        {
            CheckNullCells(false);
            foreach (var cell in cells)
            {
                if (cell.quest.IsCompleted() && !cell.quest.RewardIsReceived())
                {
                    if (alignment)
                    {
                        var point = cellsRoot.localPosition;
                        point.y = -(cell.transform as RectTransform).localPosition.y - (cell.transform as RectTransform).sizeDelta.y * 0.5f;
                        cellsRoot.localPosition = point;
                    }
                    return cell;
                }
            }
            return null;
        }

        public void Tutorial_SetLockScroll(bool value)
        {
            if (value)
            {
                scrollRect.vertical = false;
                scrollbar.interactable = false;
                scrollRect.verticalNormalizedPosition = 1f;
            }
            else
            {
                scrollRect.vertical = true;
                scrollbar.interactable = true;
            }
        }

        public bool RectTransformInScrollViewport(RectTransform rt, bool checkRect)
        {
            if (checkRect)
                return (rt.position.y - rt.rect.height) < viewportUp.position.y && (rt.position.y + rt.rect.height) > viewportBottom.position.y;
            else
                return rt.position.y < viewportUp.position.y && rt.position.y > viewportBottom.position.y;
        }


        private void UpdateAllQuestSlider()
        {
            var value = QuestManager.Instance.TotalProgressReward();
            allQuestsSlider.ChangeValue(value);
            allQuestsIsCompletedLabelGo.SetActive(value == 1f);
        }

        public void ClickNextLevel()
        {
            if (QuestManager.Instance != null && QuestManager.Instance.nextLevelEvent != null)
            {
                Hide();
                QuestManager.Instance.nextLevelEvent.Invoke();
            }
        }
    }
}
