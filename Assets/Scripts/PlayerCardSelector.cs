using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;
using System.Linq;
using DG.Tweening;

public class PlayerCardSelector : MonoBehaviour
{
    public TMP_Text playerNameText, cardInfoText;
    public GameObject turnIndicator;
    public Transform[] cardPositions;

    [SerializeField]
    Dictionary<int, int> cardPos;
    [SerializeField]
    int hiddenCardIndex = -1;
    [SerializeField]
    int firstCardIndex = 0;

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

    [SerializeField]
    private bool canMove = true;

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
        ScrollTo(cards.IndexOf(c), 0.1f);
    }

    int scrollPos = 0;
    public void ScrollTo(int index = -1, float duration = 0.3f)
    {
        if (cm.MyCards.Count <= 4 || !canMove)
            return;
        if (index != -1) // si le pasamos -1, que mire scrollPos sin actualizar el valor
        {
            scrollPos = index;
            Debug.Log("setting scrollPos to " + scrollPos);
        }
        else // debug
        {
            Debug.Log("Called from tween. We want to go to " + scrollPos + " and we are at " + firstCardIndex);
        }
        // have we reached our dest?
        if (firstCardIndex != scrollPos)
        {
            overflowAvoider++;
            if (overflowAvoider > 50)
            {
                Debug.Log("overflow!");
                return;
            }
            TweenMove(index, duration);
        }
        else // debug
        {
            overflowAvoider = 0;
            Debug.Log("finished scroll!!");
        }
    }

    // hacer que salga un popup para elegir el palo forzado del 8 (y que sea mejor una imagen, no un text input)
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
        // esto no es seguro
        var d = (firstCardIndex + (back ? -1 : 1)) % cm.MyCards.Count;
        ScrollTo(d);
        //RefreshCards();
    }

    float nfmod(float a, float b)
    {
        return a - b * Mathf.Floor(a / b);
    }

    int overflowAvoider = 0;
    public void TweenMove(int index, float duration=0.3f)
    {
        bool neg = index < firstCardIndex; // seguro?
        //float rawDst = Mathf.Abs(index - firstCardIndex);
        // float warpDst = nfmod(-(firstCardIndex - index), cm.MyCards.Count);
        // bool neg = rawDst < cm.MyCards.Count / 2; //= warpDst; // > o <?
        Debug.Log("From " + firstCardIndex + " to " + index + " . Neg: " + neg);
        if (cardPos == null)
        {
            cardPos = new Dictionary<int, int>();
            for (int i = 0; i < cardImages.Count; i++)
            {
                cardPos.Add(i, i+1); // initial values, where pos = index
            }
        }
        if (hiddenCardIndex == -1)
            hiddenCardIndex = 4;

        canMove = false;

        int newCardIndex;
        if (neg)
        {
            cardImages[hiddenCardIndex].transform.position = cardPositions[5].position;
            newCardIndex = (int)nfmod(firstCardIndex - 4, cm.MyCards.Count);
            cardPos[hiddenCardIndex] = 5;
            cardImages[hiddenCardIndex].sprite = cm.GetCardSprite(cm.MyCards[newCardIndex]);
            // This is the card that WILL be the hidden card AFTER we move
            hiddenCardIndex = cardPos.FirstOrDefault(x => x.Value == 1).Key; // the one that pos == 1
        }
        else
        {
            cardImages[hiddenCardIndex].transform.position = cardPositions[0].position;
            newCardIndex = (int)nfmod(firstCardIndex - 1, cm.MyCards.Count);
            cardPos[hiddenCardIndex] = 0;
            cardImages[hiddenCardIndex].sprite = cm.GetCardSprite(cm.MyCards[newCardIndex]);
            // This is the card that WILL be the hidden card AFTER we move
            hiddenCardIndex = cardPos.FirstOrDefault(x => x.Value == 4).Key; // the one that has pos == 4
        }
        //Debug.Log(firstCardIndex + " " + newCardIndex);
        // The index that the card at pos=0 will have AFTER we move // esto ya lo hacemos arriba // o no
        firstCardIndex = (firstCardIndex + (neg ? -1 : 1)) % cm.MyCards.Count;

        foreach (KeyValuePair<int, int> pos in cardPos)
        {
            int desiredPos = (pos.Value + (neg ? -1 : 1)) % 6;
            var t = cardPositions[desiredPos];
            var cImg = cardImages[pos.Key];

            if (pos.Value == 2) // || pos.Value == 3) card at position 2 gets obscured by other cards
            {
                cImg.transform.SetAsFirstSibling();
            }

            // Before tweening, we should set the sprite of the cards (using a linked list?)
            // Debug.Log("tweening " + pos.Value + " to " + desiredPos);
            cImg.transform.DOMove(t.position, duration);
            cImg.transform.DORotate(t.eulerAngles, duration).OnComplete(() => {
                // Update position
                cardPos[pos.Key] = desiredPos;
                canMove = true;
                // var d = (firstCardIndex + (neg ? -1 : 1)) % cm.MyCards.Count;
                ScrollTo(-1, 0.1f); // ???
            });
        }
    }
}
