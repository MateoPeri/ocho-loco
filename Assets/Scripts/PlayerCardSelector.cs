using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using System.Linq;
using DG.Tweening;

public class PlayerCardSelector : MonoBehaviour
{
    public TMP_Text playerNameText, cardInfoText;
    public GameObject turnIndicator;
    public GameObject pForzadoPopup;
    public Transform[] cardPositions;

    private Card cardToPlay;

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
    private Player ownerPlayer;
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

            for (int i = 0; i < Mathf.Max(0, cardImages.Count - cm.MyCards.Count); i++) // cI.count o 4?
            {
                cardImages[cardImages.Count - (i+1)].gameObject.SetActive(false);
                lastActiveImgIndex = cardImages.Count - (i+2);
            }
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        }
    }

    public void Initialize(Player player, int playerId, string pName, bool master) // called by card manager despúes de barajar
    {
        ownerPlayer = player;
        ownerId = playerId;
        playerName = pName;
        isMaster = master;
        cm = CardManager.Instance;
        playerNameText.text = isMaster ? playerName + " (<color=\"blue\">Master</color>)" : playerName;
        if (PhotonNetwork.LocalPlayer.ActorNumber == ownerId)
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        RefreshCards();
    }

    public void SetPlayerTurn(bool value)
    {
        isPlayerTurn = value;
    }

    public void PlayCard(Image img)
    {
        //Card c = cards[(int)nfmod(cardImages.IndexOf(img), cards.Count)];
        cardToPlay = cm.GetCardFromSprite(img.sprite);
        if (cardToPlay.num == 8)
        {
            // show popup for palo forzado
            // que el popup llame a esto -> OnCardPlayOptionsSelected(pForzado);
            pForzadoPopup.SetActive(true);
        }
        else
        {
            OnCardPlayOptionsSelected();
        }
    }

    public void ToggleTurnIndicator(bool value)
    {
        turnIndicator.SetActive(value);
    }

    private void Update()
    {
        if (!canMove)
            return;
        if (firstCardIndex != scrollPos && tweenDuration == 0.1f)
        {
            // Debug.Log("called from update");
            ScrollTo(scrollPos);
        }
        else
        {
            tweenDuration = 0.3f;
        }
    }

    int scrollPos = 0;
    public void ScrollTo(int index)
    {
        if (cm.MyCards.Count <= 4 || !canMove)
            return;
        scrollPos = index;
        // Debug.Log("setting scrollPos to " + scrollPos + ", we are at " + firstCardIndex);
        if (firstCardIndex != scrollPos)
        {
            TweenMove(index);
        }
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

    // hacer que salga un popup para elegir el palo forzado del 8 (y que sea mejor una imagen, no un text input)
    public void OnCardPlayOptionsSelected(Image input = null)
    {
        pForzadoPopup.SetActive(false);
        int pForzado = (input == null) ? -1 : cm.GetCardFromSprite(input.sprite).palo;
        Debug.Log("played " + cardToPlay);
        cm.PlayCard(cardToPlay, PhotonNetwork.LocalPlayer, pForzado);
    }

    public void MoveCards(bool back)
    {
        // esto no es seguro
        var b = cm.MyCards.Count == 5 ? !back : back;
        var d = (int)nfmod(firstCardIndex + (back ? -1 : 1), cm.MyCards.Count);
        ScrollTo(d);
        //RefreshCards();
    }

    public void Paso()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            Debug.Log("otro debería haber pasado");
            cm.NoHasPasado(PhotonNetwork.LocalPlayer, ownerPlayer); // NOOOOO pasar el player de la foto
        }
        else
        {
            Debug.Log("pasando yo");
            if (cm.IsMyTurn(PhotonNetwork.LocalPlayer))
                cm.Paso();
        }
    }

    public void VoyPorUna()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            Debug.Log("otro no ha dicho voy por 1");
            cm.NoHasDichoVP1(PhotonNetwork.LocalPlayer, ownerPlayer); // NOOOO pasar el player de la foto
        }
        else
        {
            Debug.Log("yo voy por 1");
            cm.VoyPorUna();
        }
    }

    float nfmod(float a, float b)
    {
        return a - b * Mathf.Floor(a / b);
    }

    private int GetDirection(int a, int b, int c)
    {
        if (a == b)
            return 0;
        var tmp = Mathf.Max(a, b) - Mathf.Min(a, b) < c / 2;
        if (tmp && a<b || !tmp && a > b)
           return 1;
        else
            return -1;
    }

    float tweenDuration = 0.3f;
    public void TweenMove(int index)
    {
        int dir = GetDirection(firstCardIndex, index, cm.MyCards.Count);
        if (dir == 0)
            return;
        bool neg = dir == -1;
        // Debug.Log("From " + firstCardIndex + " to " + index + " (count=" + cm.MyCards.Count + "). Neg: " + neg);
        canMove = false;

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
        //firstCardIndex = (firstCardIndex + (neg ? -1 : 1)) % cm.MyCards.Count;
        firstCardIndex = (int)nfmod((firstCardIndex + (neg ? -1 : 1)), cm.MyCards.Count);

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
            cImg.transform.DOMove(t.position, tweenDuration);
            cImg.transform.DORotate(t.eulerAngles, tweenDuration).OnComplete(() => {
                // Update position
                cardPos[pos.Key] = desiredPos;
                canMove = true;
                tweenDuration = 0.1f;
                // var d = (firstCardIndex + (neg ? -1 : 1)) % cm.MyCards.Count;
                // ScrollTo(-1, 0.1f); // ???
            });
        }
    }
}
