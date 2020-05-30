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
    public GameObject pForzadoPopup;
    public Transform cardParent;
    public GameObject cardPrefab;

    private Card cardToPlay;

    public Dictionary<Card, Image> cards;

    [Header("For other player card selector")]
    public List<Image> cardImages;

    public CardManager cm;
    private Player ownerPlayer;
    private int ownerId;
    private string playerName;
    private bool isMaster;
    private bool isPlayerTurn;

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

        Refresh();
    }

    public void SpawnCards()
    {
        foreach (Transform child in cardParent)
            Destroy(child.gameObject);

        cards = new Dictionary<Card, Image>(cm.MyCards.Count);

        foreach (var c in cm.MyCards)
        {
            var obj = Instantiate(cardPrefab, cardParent);
            //obj.transform.SetParent();
            //obj.transform.localScale = Vector3.one;

            var img = obj.GetComponent<Image>();
            img.sprite = cm.GetCardSprite(c);

            obj.GetComponent<Button>().onClick.AddListener(() => PlayCard(img));

            cards.Add(c, img);
        }
    }

    public void Refresh()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            cardImages.ForEach(x => x.sprite = cm.GetCardSprite("back_0_0"));
            cardInfoText.gameObject.SetActive(false);
            //cardInfoText.text = "<b><color=\"blue\">" + cm.MyCards.Count + "</color></b> cartas";
        }
        else
        {
            SpawnCards();
            cardInfoText.text = "Te quedan <b><color=\"green\">" + cm.MyCards.Count + "</color></b> cartas";
        }
    }

    public void SetPlayerTurn(bool value)
    {
        isPlayerTurn = value;
    }

    public void PlayCard(Image img)
    {
        // this would be better if we got the key from the image value
        cardToPlay = cm.GetCardFromSprite(img.sprite);
        Debug.Log(cardToPlay);
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
        var c = value ? Color.green : Color.white;
        c.a = 100;
        GetComponent<Image>().color = c;

    }

    // TODO
    public void ScrollTo(int index)
    {
        return;
    }

    public void ScrollToCard(Card c)
    {
        if (!cards.Keys.Contains(c))
        {
            Debug.Log("ayuda");
            return;
        }
        ScrollTo(cards.Keys.ToList().IndexOf(c));
    }

    // hacer que salga un popup para elegir el palo forzado del 8 (y que sea mejor una imagen, no un text input)
    public void OnCardPlayOptionsSelected(Image input = null)
    {
        pForzadoPopup.SetActive(false);
        int pForzado = (input == null) ? -1 : cm.GetCardFromSprite(input.sprite).palo;
        Debug.Log("played " + cardToPlay);
        cm.PlayCard(cardToPlay, PhotonNetwork.LocalPlayer, pForzado);
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
}
