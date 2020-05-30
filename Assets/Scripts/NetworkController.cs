using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Linq;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class NetworkController : MonoBehaviourPunCallbacks
{
    public static NetworkController Instance;

    public int RoomSize;
    public bool debug;

    [Header("Prefabs")]
    public GameObject roomListItemPrefab;
    public GameObject playerListItemPrefab;

    [Header("UI")]
    public TMP_Text statusText;
    public TMP_Text nickText;
    public TMP_Text roomTitleText;
    public TMP_Text playerListText;
    public TMP_InputField nickInput;
    public GameObject startButton;
    public Transform roomListParent, playerListParent;

    [Header("Panels")]
    public GameObject lobbyPanel;
    public GameObject loginPanel;
    public GameObject roomPanel;

    private Dictionary<string, RoomInfo> cachedRoomList;
    private Dictionary<int, PlayerListItem> playerList;

    private string playerName;
    private string roomName;
    private string joining;

    private void Awake()
    {
        Instance = this;
        PhotonNetwork.AutomaticallySyncScene = true;

        cachedRoomList = new Dictionary<string, RoomInfo>();

        nickInput.text = "Jugador " + Random.Range(1000, 10000);
        nickText.text = "Nombre: " + nickInput.text;
    }

    private void Start()
    {
        loginPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        roomPanel.SetActive(false);
        //StartCoroutine(UpdateRoomListView());
    }

    public void OnConnectButtonClick(GameObject input)
    {
        playerName = input.GetComponent<TMP_InputField>().text;
        nickText.text = "Nombre: " + playerName;
        if (!string.IsNullOrEmpty(playerName))
        {
            PhotonNetwork.LocalPlayer.NickName = playerName;
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            if (debug) Debug.LogError("Player Name is invalid.");
        }
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        statusText.text = "Conectado";
        if (!PhotonNetwork.InLobby)
        {
            PhotonNetwork.JoinLobby();
        }
        lobbyPanel.SetActive(true);
        loginPanel.SetActive(false);

        foreach (Transform child in roomListParent)
        {
            Destroy(child.gameObject);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (debug) Debug.Log(returnCode + " | " + message);
        //CreateRoom(roomName);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        //CreateRoom();
    }

    public override void OnLeftLobby()
    {
        cachedRoomList.Clear();
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        UpdateCachedRoomList(roomList);
        StartCoroutine(UpdateRoomListView());
    }

    private void UpdateCachedRoomList(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            // Remove room from cached room list if it got closed, became invisible or was marked as removed
            if (!info.IsOpen || !info.IsVisible || info.RemovedFromList)
            {
                if (cachedRoomList.ContainsKey(info.Name))
                {
                    cachedRoomList.Remove(info.Name);
                }

                continue;
            }

            // Update cached room info
            if (cachedRoomList.ContainsKey(info.Name))
            {
                cachedRoomList[info.Name] = info;
            }
            // Add new room info to cache
            else
            {
                cachedRoomList.Add(info.Name, info);
            }
        }
    }

    private IEnumerator UpdateRoomListView()
    {
        var rooms = cachedRoomList.Values.ToList();
        int diff = rooms.Count - roomListParent.childCount;
        for (int i = 0; i < Mathf.Abs(diff); i++)
        {
            if (diff < 0)
            {
                Destroy(roomListParent.GetChild(i).gameObject);
            }
            else
            {
                var obj = Instantiate(roomListItemPrefab);
                obj.transform.SetParent(roomListParent);
                obj.transform.localScale = Vector3.one;
            }
        }

        yield return new WaitForEndOfFrame();

        if (rooms.Count != roomListParent.childCount)
            if (debug) Debug.LogError("algo ha fallado");

        int n = 0;
        foreach (Transform child in roomListParent)
        {
            var panel = child.GetComponent<RoomListItem>();
            panel.SetText(rooms[n].Name + " (" + rooms[n].PlayerCount + "/" + rooms[n].MaxPlayers + ")"); // Sala 1 (3/4)
            n++;
        }
    }

    void CreateRoom()
    {
        CreateRoom("Sala " + Random.Range(0, 10000));
    }

    void CreateRoom(string name)
    {
        RoomOptions roomOps = new RoomOptions() { IsVisible = true, IsOpen = true, MaxPlayers = (byte)RoomSize };
        PhotonNetwork.CreateRoom(name, roomOps);
        if (debug) Debug.Log("created room " + name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        CreateRoom();
    }

    public void SetRoomNameAndJoin(string rn)
    {
        roomName = rn;
        Join();
    }

    public void OnCreateRoomButtonClick(GameObject input)
    {
        roomName = input.GetComponent<TMP_InputField>().text;
        if (string.IsNullOrEmpty(roomName))
            CreateRoom();
        else
            CreateRoom(roomName);
    }

    public void OnDirectJoinButtonClick(GameObject input)
    {
        string txt = input.GetComponent<TMP_InputField>().text;
        if (debug) Debug.Log(txt);
        PhotonNetwork.JoinRoom(txt);
    }

    public void Join()
    {
        if (string.IsNullOrEmpty(joining))
        {
            joining = roomName;
            statusText.text = "Uniéndose...";
            PlayerPrefs.SetString("PlayerName", playerName);
            if (string.IsNullOrEmpty(roomName))
                PhotonNetwork.JoinRandomRoom();
            else
            {
                RoomOptions roomOps = new RoomOptions() { IsVisible = true, IsOpen = true, MaxPlayers = (byte)RoomSize };
                PhotonNetwork.JoinOrCreateRoom(roomName, roomOps, null);
            }
        }
    }

    public override void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public void OnReadyChange()
    {
        startButton.SetActive(CheckPlayersReady());
    }

    public void OnStartButtonClick()
    {
        StartCoroutine(StartGame());
    }

    public override void OnJoinedRoom()
    {
        if (debug)
            foreach (KeyValuePair<int, Player> kvp in PhotonNetwork.CurrentRoom.Players)
                Debug.Log(string.Format("Key = {0}, Value = {1}", kvp.Key, kvp.Value));

        lobbyPanel.SetActive(false);
        roomPanel.SetActive(true);

        RefreshPlayerList();
    }

    public void OnLeaveButtonClicked()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        lobbyPanel.SetActive(true);
        roomPanel.SetActive(false);

        foreach (PlayerListItem item in playerList.Values)
        {
            Destroy(item.gameObject);
        }

        playerList.Clear();
        playerList = null;
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshPlayerList();
        /*
        GameObject entry = Instantiate(PlayerListEntryPrefab);
            entry.transform.SetParent(InsideRoomPanel.transform);
            entry.transform.localScale = Vector3.one;
            entry.GetComponent<PlayerListEntry>().Initialize(newPlayer.ActorNumber, newPlayer.NickName);

            playerListEntries.Add(newPlayer.ActorNumber, entry);

            StartGameButton.SetActive(CheckPlayersReady());
        */
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        RefreshPlayerList();
        /*
        Destroy(playerListEntries[otherPlayer.ActorNumber].gameObject);
        playerListEntries.Remove(otherPlayer.ActorNumber);

        startButton.SetActive(CheckPlayersReady());
        */
    }

    private void RefreshPlayerList()
    {
        playerList = new Dictionary<int, PlayerListItem>();

        foreach (Transform child in playerListParent)
        {
            Destroy(child.gameObject);
        }
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            GameObject obj = Instantiate(playerListItemPrefab);
            obj.transform.SetParent(playerListParent);
            obj.transform.localScale = Vector3.one;
            var item = obj.GetComponent<PlayerListItem>();
            item.Initialize(p.ActorNumber, p.NickName, p.IsMasterClient);

            if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_READY, out object isPlayerReady))
            {
                item.SetPlayerReady((bool)isPlayerReady);
            }

            playerList.Add(p.ActorNumber, item);
        }
        roomTitleText.text = PhotonNetwork.CurrentRoom.Name;
        playerListText.text = string.Format("Jugadores ({0}/{1})", PhotonNetwork.CurrentRoom.PlayerCount, PhotonNetwork.CurrentRoom.MaxPlayers);
        startButton.SetActive(CheckPlayersReady());
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber == newMasterClient.ActorNumber)
        {
            startButton.SetActive(CheckPlayersReady());
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (playerList == null)
        {
            playerList = new Dictionary<int, PlayerListItem>();
        }

        if (playerList.TryGetValue(targetPlayer.ActorNumber, out PlayerListItem item))
        {
            if (changedProps.TryGetValue(OchoLoco.PLAYER_READY, out object isPlayerReady))
            {
                item.SetPlayerReady((bool)isPlayerReady);
            }
        }

        startButton.SetActive(CheckPlayersReady());
    }

    private bool CheckPlayersReady()
    {
        if (!PhotonNetwork.IsMasterClient)
            return false;

        if (PhotonNetwork.CurrentRoom.PlayerCount < 2 && false) // TODO: remove when deploying!!!
            return false;

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            object isPlayerReady;
            if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_READY, out isPlayerReady))
            {
                if (!(bool)isPlayerReady)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(0.2f);
        if (PhotonNetwork.IsMasterClient)
        {
            if (debug) Debug.Log("starting game");
            PhotonNetwork.LoadLevel(OchoLoco.GAME_SCENE_INDEX);
        }
    }
}
