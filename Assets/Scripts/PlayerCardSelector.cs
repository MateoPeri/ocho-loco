using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System.Linq;

public class PlayerCardSelector : MonoBehaviour
{
    public TMP_Text playerNameText, cardInfoText;
    public List<Image> cardImages;
    public List<Card> cards;

    public CardManager cm;
    [SerializeField]
    private int selectedCardIndex;
    private int ownerId;
    private string playerName;
    private bool isMaster;
    private bool isPlayerTurn;

    public void RefreshCards()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            cardImages.ForEach(x => x.sprite = cm.GetCardSprite("back_0_0"));
            cardInfoText.gameObject.SetActive(false);
        }
        else
        {
            cards = new List<Card>(cardImages.Count);
            for (int i = 0; i < cm.myCards.Count; i++)
            {
                selectedCardIndex = (selectedCardIndex + 1) % cm.myCards.Count;
                if (i < 4)
                {
                    Card c = cm.myCards[selectedCardIndex];
                    cards.Insert(i, c);
                    Debug.Log(selectedCardIndex);
                    cardImages[i].sprite = cm.GetCardSprite(c);
                }
            }
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.myCards.Count + "</color></b> cartas";
        }
    }

    public void Initialize(int playerId, string pName, bool master) // called by card manager despúes de barajar
    {
        ownerId = playerId;
        playerName = pName;
        isMaster = master;
        cm = CardManager.Instance;
        playerNameText.text = isMaster ? playerName + " (<color=\"blue\">Master</color>)" : playerName;
        if (PhotonNetwork.LocalPlayer.ActorNumber == ownerId)
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.myCards.Count + "</color></b> cartas";

        RefreshCards();
    }

    public void SetPlayerTurn(bool value)
    {
        isPlayerTurn = value;
    }

    public void PlayCard(Image img)
    {
        Card c = cards[cardImages.IndexOf(img)];
        if (c.num == 8)
        {
            // show popup for palo forzado
            // que el popup llame a esto -> OnCardPlayOptionsSelected(pForzado);
        }
        OnCardPlayOptionsSelected(c);
    }

    public void OnCardPlayOptionsSelected(Card c, TMP_InputField input = null)
    {
        int pForzado = (input == null) ? -1 : int.Parse(input.text);
        cm.TryPlayCard(c, PhotonNetwork.LocalPlayer, pForzado);
    }

    public void MoveCards(bool back)
    {
        Debug.Log(selectedCardIndex);
        selectedCardIndex += back ? -1 : 1;
        Debug.Log(selectedCardIndex);
        RefreshCards();
    }
}
