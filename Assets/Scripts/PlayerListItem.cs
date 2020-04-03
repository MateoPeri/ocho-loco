using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using ExitGames.Client.Photon;

public class PlayerListItem : MonoBehaviour
{
    public TMP_Text playerNameText;
    public Button playerReadyButton;
    public GameObject masterImage;

    private int ownerId;
    private bool isPlayerReady;
    private string playerName;
    private bool isMaster;

    public void Start()
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != ownerId)
        {
            playerReadyButton.gameObject.SetActive(false);
            GetComponent<Image>().color = Color.white;
        }
        else
        {
            Hashtable initialProps = new Hashtable() { { OchoLoco.PLAYER_READY, isPlayerReady } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(initialProps);

            GetComponent<Image>().color = Color.green;
            playerReadyButton.onClick.AddListener(() =>
            {
                isPlayerReady = !isPlayerReady;
                SetPlayerReady(isPlayerReady);

                Hashtable props = new Hashtable() { { OchoLoco.PLAYER_READY, isPlayerReady } };
                PhotonNetwork.LocalPlayer.SetCustomProperties(props);

                if (PhotonNetwork.IsMasterClient)
                {
                    FindObjectOfType<NetworkController>().OnReadyChange();
                }
            });
        }
    }

    public void Initialize(int playerId, string pName, bool master)
    {
        ownerId = playerId;
        playerName = pName;
        isMaster = master;
        playerNameText.text = isMaster ? playerName + " (<color=\"blue\">Master</color>)" : playerName;
    }

    public void SetPlayerReady(bool playerReady)
    {
        playerReadyButton.GetComponentInChildren<TMP_Text>().text = playerReady ? "Listo!" : "Listo?";
        masterImage.SetActive(playerReady);
    }
}