using System.Collections;
using System.Collections.Generic;
using ToolBox;
using UnityEngine;
using UnityEngine.UI;

public class UI_TournamentLeaderboard : MonoBehaviour
{
    [SerializeField] TMPro.TextMeshProUGUI stageInfoTMP;
    [SerializeField] TMPro.TextMeshProUGUI remainingMembersTMP;
    [SerializeField] Transform slots_parent;
    [SerializeField] UI_TournamentMemberSlot slot_prefab;
    [SerializeField] Button next_button;
    [SerializeField] Button close_button;
    [SerializeField] GameObject tutorFinger;
    [SerializeField] GameObject tutorialHelper;
    [SerializeField] GameObject tutorialText;
    private bool isTutorShowed = false;
    private bool isCoroutineStarted = false;

    List<UI_TournamentMemberSlot> members_slots = new List<UI_TournamentMemberSlot>();

    private void Start()
    {
        next_button.onClick.RemoveAllListeners();
        next_button.onClick.AddListener(() =>
        {
            next_button.interactable = false;
            if (TutorialManager.Instance)
            {
                if (TutorialManager.Instance.currentState is TutorialState_Level6_Tournament)
                    TutorialManager.Instance.NextState();
            }
            TournamentManager.Instance.StartNextStage();
        });

        if (TutorialManager.Instance && TutorialManager.Instance.currentState is TutorialState_Level6_Tournament)
        {
            close_button.gameObject.SetActive(false);
        }
        else
        {
            close_button.gameObject.SetActive(true);
        }

        close_button.onClick.RemoveAllListeners();
        close_button.onClick.AddListener(() =>
        {
            TournamentManager.Instance.CancelTournament_GettingReward();
        });
    }

    public void Open(List<TournamentMember> full_list, List<TournamentMember> winners, bool can_continue)
    {
        stageInfoTMP.text = TournamentManager.Instance.TournamentStage.ToString() + "/5";
        remainingMembersTMP.text = full_list.Count.ToString();

        var old_slots = slots_parent.gameObject.GetComponentsInChildren<UI_TournamentMemberSlot>();
        foreach (var slot in old_slots)
            Destroy(slot.gameObject);

        int place = 1;
        members_slots.Clear();

        foreach (var member in full_list)
        {
            var clone = Instantiate(slot_prefab, slots_parent);
            clone.placeTMP.text = place.ToString();
            if (member.isPlayer)
                member.name = TournamentMember.defaultPlayerName;
            clone.nameTMP.text = member.name;
            clone.scoresTMP.text = member.scores.ToString("0.0");
            clone.member = member;
            if (member.isPlayer || member == TournamentManager.Instance.player || member.name == TournamentMember.defaultPlayerName)
                clone.back_image.sprite = clone.player_back;
            place++;
            members_slots.Add(clone);
        }

        bool final = TournamentManager.Instance.TournamentStage >= 5;
        next_button.interactable = can_continue || final;
        close_button.gameObject.SetActive(!final);

        if (final)
        {
            next_button.onClick.RemoveAllListeners();
            next_button.onClick.AddListener(() =>
            {
                TournamentManager.Instance.CancelTournament_GettingReward();
            });
        }
        else
        {
            next_button.onClick.RemoveAllListeners();
            next_button.onClick.AddListener(() =>
            {
                next_button.interactable = false;
                if (TutorialManager.Instance)
                {
                    if (TutorialManager.Instance.currentState is TutorialState_Level6_Tournament)
                        TutorialManager.Instance.NextState();
                }
                TournamentManager.Instance.StartNextStage();
            });
        }

        gameObject.SetActive(true);

        if (!isCoroutineStarted)
        {
            GameManager.Instance.StartCoroutine(ShowRaitingAlignment_Coroutine());
        }

        if (GameManager.Instance.playerData.lastTutorialState == typeof(TutorialState_Level6_Tournament).ToString())
        {
            tutorialHelper.SetActive(true);

            if (!isTutorShowed)
            {
                isTutorShowed = true;
                tutorialText.gameObject.SetActive(true);
            }
            else tutorialText.gameObject.SetActive(false);
        }
        else
        {
            tutorialHelper.SetActive(false);
        }
    }

    IEnumerator ShowRaitingAlignment_Coroutine()
    {
        isCoroutineStarted = true;
        yield return new WaitForSeconds(0.5f);

        if (GameManager.Instance.playerData.lastTutorialState ==
            typeof(TutorialState_Level6_Tournament).ToString()
            && next_button.interactable)
        {
            tutorFinger.SetActive(true);
        }
        else tutorFinger.SetActive(false);

        List<UI_TournamentMemberSlot> winners_slots = new List<UI_TournamentMemberSlot>();
        List<TournamentMember> winners = new List<TournamentMember>();

        foreach (var slot in members_slots)
        {
            slot.placeTMP.text = slot.member.rating.ToString();
            if (slot.member.rating == 1) slot.back_image.sprite = slot.gold_back;
            if (slot.member.rating == 2) slot.back_image.sprite = slot.silver_back;
            if (slot.member.isPlayer || slot.member == TournamentManager.Instance.player || slot.member.name == TournamentMember.defaultPlayerName) slot.back_image.sprite = slot.player_back;
            if (!slot.member.win)
            {
                slot.animator.SetTrigger("left_shift");
            }
            else
            {
                winners_slots.Add(slot);
                winners.Add(slot.member);
            }
        }
        remainingMembersTMP.text = winners_slots.Count.ToString();

        float max_timer = 0.7f;
        float timer = 0.0f;

        while (timer < max_timer)
        {
            foreach (var slot in members_slots)
            {
                yield return new WaitForEndOfFrame();
                timer += Time.deltaTime;
                if (!slot.member.win)
                {
                    slot.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.0f, 0.0f, 1.0f), timer / max_timer);
                }
            }
        }

        yield return new WaitForSeconds(0.3f);

        List<UI_TournamentMemberSlot> sorting_slots = new List<UI_TournamentMemberSlot>();
        while (sorting_slots.Count < winners_slots.Count)
        {
            var clone = Instantiate(slot_prefab, slots_parent);
            Destroy(clone.animator);
            clone.rect_transform.sizeDelta = new Vector2(clone.rect_transform.sizeDelta.x, -20.0f);//20 - spacing â LayoutGroup
            clone.content.gameObject.SetActive(false);
            sorting_slots.Add(clone);
            clone.name += "_sorting";
        }

        foreach (var slot in winners_slots)
        {
            slot.transform.SetParent(null);
            slot.transform.SetParent(slots_parent);
            slot.gameObject.name += "_win";
        }

        int i, count = winners_slots.Count;
        for (i = 0; i < count; i++)
        {
            int index = winners.GetMinRatingIndex();
            var work_slot = winners_slots[index];
            winners.RemoveAt(index);
            winners_slots.RemoveAt(index);

            timer = 0.0f;
            max_timer = 0.2f;

            Vector2 target_size = new Vector2(work_slot.rect_transform.sizeDelta.x, work_slot.rect_transform.sizeDelta.y);
            Vector2 target_size_work_slot = new Vector2(work_slot.rect_transform.sizeDelta.x, -20.0f);//20 - spacing â LayoutGroup
            Destroy(work_slot.animator);
            work_slot.content.gameObject.name += "_sorting";
            work_slot.content.SetParent(sorting_slots[i].content.parent);
            var point = work_slot.transform.localPosition;
            point.x = sorting_slots[i].content.transform.localPosition.x;
            work_slot.transform.localPosition = point;
            work_slot.content.localScale = Vector3.one;

            while (timer < max_timer && index != work_slot.member.rating - 1)
            {
                yield return new WaitForEndOfFrame();
                timer += Time.deltaTime;
                var percent = timer / max_timer;
                sorting_slots[i].rect_transform.sizeDelta = Vector2.Lerp(sorting_slots[i].rect_transform.sizeDelta,
                    target_size, percent);
                work_slot.content.localPosition = Vector3.Lerp(
                    work_slot.content.localPosition, sorting_slots[i].content.localPosition, percent);
                work_slot.rect_transform.sizeDelta = Vector2.Lerp(work_slot.rect_transform.sizeDelta,
                    target_size_work_slot, percent);
            }
            sorting_slots[i].rect_transform.sizeDelta = target_size;
            work_slot.rect_transform.sizeDelta = target_size_work_slot;
            work_slot.content.localPosition = sorting_slots[i].content.localPosition;
        }

        isCoroutineStarted = false;
    }
}
