using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

//-------------------------------- ПРОСТАЯ СИСТЕМА ПРОГРАММИРОВАНИЯ ПОВЕДЕНИЯ NPC

public class BehaviourSystem : MonoBehaviour
{
//....
}

[System.Serializable]
public class TaskStep
{
    public UnityAction start_func;
    public UnityAction process_func;
    public UnityAction end_func;
    public UnityAction init_from_save_data_func;
    public bool complete = false; // должна изменяться только в process_funk, иначе самостоятельно проверяем CheckEnd()
    bool is_start = false;
    bool is_end = false;

    public void Process()
    {
        CheckStart();
        if (process_func != null) process_func();
        if (complete) CheckEnd();
    }

    public void CheckStart()
    {
        if (!is_start)
        {
            is_start = true;
            if (start_func != null) start_func();
        }
    }

    public void CheckEnd()
    {
        if (!is_end)
        {
            is_end = true;
            if (end_func != null) end_func();
        }
    }

    public void Reset()
    {
        is_start = false;
        complete = false;
        is_end = false;
    }

    public void ForceComplete()
    {
        is_start = true;
        complete = true;
        is_end = true;
    }
}

public static class TaskStep_Extension
{
    public static int Process(this List<TaskStep> list, UnityAction finish_action)
    {
        int index = 0;
        foreach (var data in list)
        {
            if (!data.complete)
            {
                data.Process();
                return index;
            }
            index++;
        }
        if (finish_action != null) finish_action();
        return index;
    }

    public static void ResetAll(this List<TaskStep> list)
    {
        foreach (var data in list)
        {
            data.Reset();
        }
    }

    public static bool AllReady(this List<TaskStep> list)
    {
        foreach (var data in list)
        {
            if (!data.complete) return false;
        }
        return true;
    }

    public static void ApplyAll(this List<TaskStep> list)
    {
        foreach (var data in list)
        {
            data.ForceComplete();
        }
    }
}
