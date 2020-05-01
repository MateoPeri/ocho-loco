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
    public GameObject turnIndicator;
    public List<Image> cardImages;
    public List<Card> cards;

    public CardManager cm;
    [SerializeField]
    private int selectedCardIndex;
    private int ownerId;
    private string playerName;
    private bool isMaster;
    private bool isPlayerTurn;
    private int lastActiveImgIndex;

    public void RefreshCards()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            cardImages.ForEach(x => x.sprite = cm.GetCardSprite("back_0_0"));
            cardInfoText.gameObject.SetActive(false);
            //cardInfoText.text = "<b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        }
        else
        {
            cards = new List<Card>(cardImages.Count);
            for (int i = 0; i < cm.MyCards.Count; i++)
            {
                selectedCardIndex = (selectedCardIndex + 1) % cm.MyCards.Count;
                if (i < 4)
                {
                    Card c = cm.MyCards[selectedCardIndex];
                    cards.Insert(i, c);
                    cardImages[i].gameObject.SetActive(true);
                    cardImages[i].sprite = cm.GetCardSprite(c);
                }
            }

            for (int i = 0; i < Mathf.Max(0, 4 - cm.MyCards.Count); i++)
            {
                cardImages[cardImages.Count - (i+1)].gameObject.SetActive(false);
                lastActiveImgIndex = cardImages.Count - (i+2);
            }
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        }
    }

    public void Initialize(int playerId, string pName, bool master) // called by card manager despúes de barajar
    {
        ownerId = playerId;
        playerName = pName;
        isMaster = master;
        cm = CardManager.Instance;
        playerNameText.text = isMaster ? playerName + " (<color=\"blue\">Master</color>)" : playerName;
        //Debug.Log("Cm is: " + (cm == null));
        //Debug.Log("Cm.Mycards is: " + (cm.MyCards == null));
        if (PhotonNetwork.LocalPlayer.ActorNumber == ownerId)
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        // else
        //    cardInfoText.text = "<b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        RefreshCards();
    }

    public void SetPlayerTurn(bool value)
    {
        isPlayerTurn = value;
    }

    public void PlayCard(Image img)
    {
        Card c = cards[cardImages.IndexOf(img) % cards.Count];
        if (c.num == 8)
        {
            // show popup for palo forzado
            // que el popup llame a esto -> OnCardPlayOptionsSelected(pForzado);
        }
        OnCardPlayOptionsSelected(c);
    }

    public void ToggleTurnIndicator(bool value)
    {
        turnIndicator.SetActive(value);
    }

    public void ScrollToCard(Card c)
    {
        if (!cards.Contains(c))
        {
            Debug.Log("ayuda");
            return;
        }
        ScrollTo(cards.IndexOf(c));
    }

    public void ScrollTo(int index)
    {
        // hacer que scrollee hasta encontrar el index deseado
        Debug.Log(index); // TODO: hacer que esto del scroll funcione
        bool back = selectedCardIndex - index > 0;
        var s = cm.GetCardSprite(cards[index]);
        while (cardImages[lastActiveImgIndex].sprite != s) // da error cuando es -1, cuando ya no quedan cartas
        {
            MoveCards(back);
        }
    }

    public void OnCardPlayOptionsSelected(Card c, TMP_InputField input = null)
    {
        int pForzado = (input == null) ? -1 : int.Parse(input.text);
        if (cm.CanPlayCard(c) || true) // TODO: eliminar el debug
        {
            cm.PlayCard(c, PhotonNetwork.LocalPlayer, pForzado, true);
        }
    }

    public void MoveCards(bool back)
    {
        selectedCardIndex += back ? -1 : 1;
        RefreshCards();
    }
}
