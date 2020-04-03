using UnityEngine;
using TMPro;
using Photon.Pun;

public class RoomListItem : MonoBehaviour
{
    public TMP_Text text;
    private NetworkController netController;
    private string roomName;
    
    public void SetText(string newText)
    {
        text.text = newText;
    }


    public void OnClick()
    {
        if (PhotonNetwork.InLobby)
        {
            PhotonNetwork.LeaveLobby();
        }

        NetworkController.Instance.SetRoomNameAndJoin(roomName);
    }

    public void Init(string name, byte currentPlayers, byte maxPlayers)
    {
        roomName = name;
        text.text = roomName + " " + currentPlayers + " / " + maxPlayers;
    }
}
