using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TournamentManager : MonoBehaviour
{
    [SerializeField] List<TournamentScene> scenes_stack = new List<TournamentScene>();
    [SerializeField] List<TournamentScene> scenes_stack_for_final = new List<TournamentScene>();
    [SerializeField] List<TournamentMember> members_stack = new List<TournamentMember>();
    public TournamentMember player;
    private Tournament_SaveData tourPlData => GameManager.Instance.playerData.tournament;


    [Header("UI")]
    public UI_StartTournamentWindow startTournamentWindow;
    public TournamentRandomMapWindow randomSceneWindow;
    public UI_TournamentLeaderboard leaderboardWindow;
    public UI_TournamentRewardWindow tournamentRewardWindow;

    public SO_ResourcesSet reward_top_1;
    public SO_ResourcesSet reward_top_2;
    public SO_ResourcesSet reward_top_3;
    public SO_ResourcesSet reward_top_4;
    public SO_ResourcesSet reward_top_5;

    private static TournamentManager instance;
    public static TournamentManager Instance => instance;

    int tournament_stage = 0;
    public int TournamentStage { get => tournament_stage; }
    SO_ResourcesSet current_reward;
    bool last_stage_result_win = false;

    List<TournamentMember> generated_members = new List<TournamentMember>();
    List<TypeMechanics> all_types = new List<TypeMechanics>();


    private void Awake()
    {
        if (instance == null) instance = this;
        else
        {
            Destroy(this);
            return;
        }

        foreach (var data in scenes_stack)
            if (!all_types.Contains(data.type))
                all_types.Add(data.type);
    }

    public void CheckTournament()
    {
        if (tourPlData.need_continue_tournament)
        {
            if (generated_members == null || generated_members.Count == 0)
            {
                generated_members = new List<TournamentMember>();
                generated_members.AddRange(tourPlData.generated_members);

                TournamentMember item_player = null;
                foreach (var item in generated_members)
                {
                    if (item.name == player.name)
                    {
                        item_player = item;
                        break;
                    }
                }
                if (item_player != null)
                {
                    generated_members.Remove(item_player);
                    generated_members.Add(player);
                }
            }

            player.rating = tourPlData.rating;
            player.scores = tourPlData.scores;
            player.win = tourPlData.win;
            tournament_stage = tourPlData.tournament_stage;
            last_stage_result_win = tourPlData.last_result_tournament_stage_win;
            if (tournament_stage < 0) tournament_stage = 0;
            if (tournament_stage > 5) tournament_stage = 5;
            if (tournament_stage == 0) StartNextStage();
            else NextStage(0, player.win, true);
        }
    }

    public bool CanStartTournament()
    {
        return (GameManager.Instance.playerData.tournament.CanStartTournament()
            || GameManager.Instance.playerData.tournament.CanTryAgainTournament());
    }

    public void StartTournament()
    {
        if (!CanStartTournament()) return;
        GameManager.Instance.playerData.tournament.RegisterStart();
        GameManager.Instance.playerData.SaveData();

        generated_members.Clear();
        player.rating = 0;
        player.scores = 0;
        player.win = false;

        tourPlData.rating = 0;
        tourPlData.scores = 0;
        tourPlData.win = false;

        GenerateRivals();

        tournament_stage = 0;
        tourPlData.tournament_stage = tournament_stage;
        last_stage_result_win = false;
        StartNextStage();
    }

    private void GenerateRivals()
    {
        int i;
        List<TournamentMember> work_list = new List<TournamentMember>();
        work_list.AddRange(members_stack);

        for (i = 0; i < 31; i++)
        {
            var random_member = work_list.GetRandomItem();
            generated_members.Add(random_member);
            work_list.Remove(random_member);
        }
        player.isPlayer = true;
        generated_members.Add(player);
        generated_members.SortingByName_MinMax();

        tourPlData.generated_members.Clear();
        tourPlData.generated_members.AddRange(generated_members);
        GameManager.Instance.playerData.SaveData();
    }

    public void NextStage(int level_stars, bool win, bool is_resume = false)
    {
        List<TournamentMember> old_list = new List<TournamentMember>();

        old_list.AddRange(generated_members);
        last_stage_result_win = win;
        player.scores += level_stars * 0.5f;
        player.win = win;

        switch (tournament_stage)
        {
            case 1:
                current_reward = reward_top_5;
                if (!is_resume) CalculateRating(win, 1, player.scores, 16);
                leaderboardWindow.Open(old_list, generated_members, win);
                break;
            case 2:
                if (win) current_reward = reward_top_4;
                else current_reward = reward_top_5;
                if (!is_resume) CalculateRating(win, 2, player.scores, 8);
                leaderboardWindow.Open(old_list, generated_members, win);
                break;
            case 3:
                if (win) current_reward = reward_top_3;
                else current_reward = reward_top_4;
                if (!is_resume) CalculateRating(win, 3, player.scores, 4);
                leaderboardWindow.Open(old_list, generated_members, win);
                break;
            case 4:
                if (win) current_reward = reward_top_2;
                else current_reward = reward_top_3;
                if (!is_resume) CalculateRating(win, 4, player.scores, 2);
                leaderboardWindow.Open(old_list, generated_members, win);
                break;
            case 5:
                if (win) current_reward = reward_top_1;
                else current_reward = reward_top_2;
                if (!is_resume) CalculateRating(win, 5, player.scores, 1);
                leaderboardWindow.Open(old_list, generated_members, win);
                break;
        }

        tourPlData.tournament_stage = tournament_stage;
        tourPlData.rating = player.rating;
        tourPlData.scores = player.scores;
        tourPlData.win = player.win;
        tourPlData.last_result_tournament_stage_win = last_stage_result_win;
        tourPlData.generated_members.Clear();
        tourPlData.generated_members.AddRange(generated_members);
        GameManager.Instance.playerData.SaveData();
    }

    public void CalculateRating(bool player_win, int win_stars, float player_stars, int count_remaining)
    {
        List<TournamentMember> work_list = new List<TournamentMember>();
        int max_stars = 6;
        int i;
        float min_win_stars = player_stars;

        work_list.AddRange(generated_members);
        work_list.Remove(player);
        generated_members.Clear();

        if (player_win) generated_members.Add(player);

        while (generated_members.Count < count_remaining)
        {
            var random_member = work_list.GetRandomItem();
            var stars = Random.Range(win_stars, max_stars + 1);
            random_member.scores += stars * 0.5f;
            if (player_win) random_member.scores = Mathf.Clamp(random_member.scores,
                Mathf.Max(random_member.scores, player_stars - win_stars * 0.5f), player_stars + win_stars * 0.5f);
            else random_member.scores = Mathf.Clamp(random_member.scores,
                player_stars + 0.5f, random_member.scores);
            random_member.win = true;
            generated_members.Add(random_member);
            work_list.Remove(random_member);

            if (random_member.scores < min_win_stars) min_win_stars = random_member.scores;
        }

        foreach (var loser in work_list)
        {
            var stars = Random.Range(0, win_stars);
            loser.scores += stars * 0.5f;
            loser.scores = Mathf.Clamp(loser.scores, loser.scores, min_win_stars - 0.5f);
            loser.win = false;
        }

        if (!player_win) work_list.Add(player);

        List<TournamentMember> full_list = new List<TournamentMember>();
        full_list.AddRange(generated_members);
        full_list.AddRange(work_list);
        full_list.SortingByScores_MaxMin();
        for (i = 0; i < full_list.Count; i++)
            full_list[i].rating = i + 1;

        tourPlData.generated_members.Clear();
        tourPlData.generated_members.AddRange(generated_members);
        GameManager.Instance.playerData.SaveData();
    }

    public void StartNextStage()
    {
        OffAllWindows();

        tournament_stage++;

        TypeMechanics target_mechanics;

        if (tournament_stage == 5)
        {
            if (Random.Range(0.0f, 1.0f) <= 0.5f) target_mechanics = TypeMechanics.Vert_Challenge;
            else target_mechanics = TypeMechanics.Horis_Challenge;
        }
        else
        {
            if (all_types.Count > 0)
                target_mechanics = all_types[Random.Range(0, all_types.Count)];
            else target_mechanics = TypeMechanics.Bark;
        }

        startTournamentWindow.gameObject.SetActive(false);
        randomSceneWindow.Open(target_mechanics);
    }

    public void PlayRandomMechanicsScene(TypeMechanics target_type)
    {
        var mechanics_list = new List<string>();
        List<TournamentScene> _scenes_stack = scenes_stack;

        if (tournament_stage == 5) _scenes_stack = scenes_stack_for_final;

        foreach (var data in _scenes_stack)
            if (data.type == target_type)
                mechanics_list.Add(data.scene_name);

        if (mechanics_list.Count == 0)
        {
            Debug.LogError("Mechanics list is empty!");
            return;
        }
        else
        {
            startTournamentWindow.gameObject.SetActive(false);
            randomSceneWindow.gameObject.SetActive(false);
            if (all_types.Contains(target_type)) all_types.Remove(target_type);
            ScenesLoader.Instance.LoadScene(mechanics_list[Random.Range(0, mechanics_list.Count)], TypeScene.Tournament);
        }
    }

    public void CancelTournament_GettingReward()
    {
        GameManager.Instance.playerData.tournament.need_continue_tournament = false;
        startTournamentWindow.gameObject.SetActive(false);
        randomSceneWindow.gameObject.SetActive(false);
        leaderboardWindow.gameObject.SetActive(false);
        int place = player.rating;// tourPlData.rating;
        if (last_stage_result_win && tournament_stage < 5) place = generated_members.Count + 1;
        tournamentRewardWindow.Open(last_stage_result_win, current_reward, player, place);
    }

    public void ApplyRewardCurrentStage(float multiplier = 1.0f)
    {
        GameManager.Instance.playerData.Achievements().tournament_completed = true;

        UI_RewardEffect.lastCompletedSceneType = TypeScene.Tournament;
        UI_RewardEffect.old_data = GameManager.Instance.playerData.GetFullCollectedResources();

        if (tournament_stage == 5) GameManager.Instance.playerData.tournament.use_try_again = true;

        GameManager.Instance.playerData.tournament.last_result_tournament_win = tournament_stage == 5;
        SO_ResourcesSet reward = current_reward.Multiplier(multiplier);
        GameManager.Instance.playerData.AddResources(reward.ToResourcesList());
        GameManager.Instance.playerData.tournament.tournament_finish = true;
        GameManager.Instance.playerData.tournament.need_continue_tournament = false;

        if (SkinSystem.current_tournament_skin != null)
            SkinSystem.current_tournament_skin.Unlock(false);
        GameManager.Instance.playerData.tool_skins_data.CheckTargetSkins();
        GameManager.Instance.playerData.SaveData();
        FinishTournament();
    }

    public void FinishTournament()
    {
        OffAllWindows();
        ScenesLoader.Instance.GotoStateMap();
    }

    public void OffAllWindows()
    {
        startTournamentWindow.gameObject.SetActive(false);
        randomSceneWindow.gameObject.SetActive(false);        
        tournamentRewardWindow.gameObject.SetActive(false);
        StartCoroutine(HideLeaderBoard());
    }

    private IEnumerator HideLeaderBoard()
    {
        yield return new WaitForSecondsRealtime(0.1f);
        leaderboardWindow.gameObject.SetActive(false);
    }

    public void Check_RewardEffect()
    {
        if (!GameManager.Instance.playerData.tournament.tournament_finish) return;
        if (UI_RewardEffect.lastCompletedSceneType == TypeScene.Tournament && current_reward != null)
        {
            UI_RewardEffect.ShowRewardEffect_LocalGlobalMap(current_reward, Vector2.zero);
            UI_RewardEffect.lastCompletedSceneType = TypeScene.None;
            current_reward = null;
        }
    }

    public string StageLabel()
    {
        switch (tournament_stage)
        {
            case 1: return "Round of 32";
            case 2: return "Round of 16";
            case 3: return "Round of 8";
            case 4: return "Semifinals";
            case 5: return "Finals";
        }
        return "Tournament";
    }

    public float CurrentPercent()
    {
        return (float)(tournament_stage - 1.0f) / 5.0f;
    }

    public float NextPercent()
    {
        return (float)tournament_stage / 5.0f;
    }
}

[System.Serializable]
public class Scene_Reward
{
    public string scene_name = "";
    public List<ResourceCount> reward;
}

[System.Serializable]
public class TournamentMember
{
    public const string defaultPlayerName = "Jack";
    public string name = defaultPlayerName;
    public bool isPlayer = false;
    [HideInInspector] public float scores = 0;
    [HideInInspector] public int rating = 0;
    [HideInInspector] public bool win = false;
}


public static class TournamentMember_Extension
{
    public static void SortingByName_MinMax(this List<TournamentMember> list)
    {
        if (list.Count == 0) return;

        List<TournamentMember> work_list = new List<TournamentMember>();
        work_list.AddRange(list);
        list.Clear();

        int i, count = work_list.Count;
        for (i = 0; i < count; i++)
        {
            var index = work_list.GetMinimalNameIndex();
            list.Add(work_list[index]);
            work_list.RemoveAt(index);
        }
    }

    public static int GetMinimalNameIndex(this List<TournamentMember> list)
    {
        string minimal = "";
        int i = -1;
        int minimal_index = -1;
        if (list.Count > 0)
        {
            minimal_index = 0;
            minimal = list[0].name;
        }

        for (i = 1; i < list.Count; i++)
            if (list[i].name.CompareTo(minimal) < 0)
            {
                minimal = list[i].name;
                minimal_index = i;
            }
        return minimal_index;
    }

    public static void SortingByScores_MaxMin(this List<TournamentMember> list)
    {
        if (list.Count == 0) return;

        List<TournamentMember> work_list = new List<TournamentMember>();
        work_list.AddRange(list);
        list.Clear();

        int i, count = work_list.Count;
        for (i = 0; i < count; i++)
        {
            var index = work_list.GetMaxScoreIndex();
            list.Add(work_list[index]);
            work_list.RemoveAt(index);
        }
    }

    public static int GetMaxScoreIndex(this List<TournamentMember> list)
    {
        float max = 0.0f;
        int i;
        int max_index = -1;
        if (list.Count > 0)
        {
            max_index = 0;
            max = list[0].scores;
        }

        for (i = 1; i < list.Count; i++)
            if (list[i].scores > max)
            {
                max = list[i].scores;
                max_index = i;
            }
        return max_index;
    }

    public static int GetMinRatingIndex(this List<TournamentMember> list)
    {
        int min = 1;
        int i;
        int min_index = -1;
        if (list.Count > 0)
        {
            min_index = 0;
            min = list[0].rating;
        }

        for (i = 1; i < list.Count; i++)
            if (list[i].rating < min)
            {
                min = list[i].rating;
                min_index = i;
            }
        return min_index;
    }

    public static TournamentMember GetMinRatingItem(this List<TournamentMember> list)
    {
        int min = 32;
        int i = -1;
        TournamentMember min_item = null;

        if (list.Count > 0)
        {
            min_item = list[0];
            min = list[0].rating;
        }

        for (i = 1; i < list.Count; i++)
            if (list[i].rating < min)
            {
                min = list[i].rating;
                min_item = list[i];
            }
        return min_item;
    }
}


[System.Serializable]
public class TournamentScene
{
    public string scene_name = "";
    public TypeMechanics type = TypeMechanics.Branch;
}
