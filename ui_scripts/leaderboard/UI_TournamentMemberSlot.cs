using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_TournamentMemberSlot : MonoBehaviour
{
    public RectTransform rect_transform;
    public Animator animator;
    public Transform content;
    public TMPro.TextMeshProUGUI placeTMP;
    public TMPro.TextMeshProUGUI nameTMP;
    public TMPro.TextMeshProUGUI scoresTMP;
    public Image back_image;
    [Header("Sprites")]
    public Sprite gold_back;
    public Sprite silver_back;
    public Sprite player_back;

    [HideInInspector] public TournamentMember member;
}
