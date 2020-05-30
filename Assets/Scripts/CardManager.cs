using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;
using System.Collections;

public class CardManager : PunTurnManager, IPunObservable, IPunTurnManagerCallbacks
{
    public static CardManager Instance;
    public Console console;

    public List<Sprite> cardSprites = new List<Sprite>();
    public List<Card> allCards;
    public Queue<Card> remainingCards; // cartas del montón (para robar)
    public Stack<Card> playingCardStack; // el montón con el que se juega

    public GameObject gamePanel, winPanel;

    [Header("WIN")]
    public TMP_Text[] podium;

    List<Card>[] playerCardList; // una lista de listas para guardar las cartas de los jugadores

    public GameObject playerCardSelectorPrefab, otherPlayerSelectorPrefab;
    public Transform pcsParent;

    public Image stealingStack, playingStack, paloImage;
    public TMP_Text stealingStackText;

    private Dictionary<int, PlayerCardSelector> playerCardSelectors;

    public int pasarPenal = 3;
    public int vp1Penal = 3;
    public static string[] palos = new string[4] { "picas", "corazones", "diamantes", "tréboles"};

    private int pf;
    public int paloForzado
    {
        get { return pf; }
        set
        {
            pf = value;
            paloImage.sprite = GetCardSprite("cards_" + value + "_1");
        }
    }

    public List<Card> MyCards;

    public int nBarajas = 1;
    public bool isPlayerTurn;

    List<KeyValuePair<Player, object>> turnHistory = new List<KeyValuePair<Player, object>>();

    public bool debug, cheat;

    public int playerTurnIndex;

    private void Awake()
    {
        Instance = this;
        // photonView = GetComponent<PhotonView>();
        PhotonPeer.RegisterType(typeof(Card), (byte)'C', Card.SerializeCard, Card.DeserializeCard);
        TurnManagerListener = this;
        TurnDuration = -1f;
    }

    private void Start()
    {
        console = Console.Instance;
        gamePanel.SetActive(true);
        winPanel.SetActive(false);
        if (PhotonNetwork.IsMasterClient)
        {
            allCards = new List<Card>();
            foreach (Sprite sprite in cardSprites)
            {
                Card c = GetCardFromSprite(sprite);
                if (c.num == 0) // cartas especiales
                    continue;
                allCards.Add(c);
            }
            nBarajas = Mathf.Max(1, Mathf.CeilToInt(PhotonNetwork.CurrentRoom.PlayerCount / 4)); // 1 baraja por cada 4 jugadores --> 20 cartas libres

            remainingCards = new Queue<Card>(allCards);
            playingCardStack = new Stack<Card>();
            playerCardList = RepartirCartas(nBarajas);
            playingCardStack.Push(remainingCards.Dequeue());
            paloForzado = playingCardStack.Peek().palo;

            SyncRoomCards();
            BeginTurn();
        }
    }

    private void Update()
    {
        // Cheats
        if (Input.GetKeyDown(KeyCode.K))
        {
            AddCard(PhotonNetwork.LocalPlayer, new Card[1] { new Card(0, 2) });
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            AddCard(PhotonNetwork.LocalPlayer, new Card[1] { new Card(0, 8) });
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            cheat = true;
        }
    }

    public void LeaveRoom()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause == DisconnectCause.DisconnectByClientLogic)
            UnityEngine.SceneManagement.SceneManager.LoadScene(OchoLoco.MAIN_SCENE_INDEX);
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.Disconnect();
        console.WriteLine("Has salido de la sala.");
    }

    public List<Card>[] RepartirCartas(int n = 1)
    {
        var rnd = new System.Random();
        remainingCards = new Queue<Card>(remainingCards.ToList().Shuffle());

        List<Card>[] pcl = new List<Card>[PhotonNetwork.CurrentRoom.PlayerCount];

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < pcl.Length; j++)
            {
                if (pcl[j] == null)
                    pcl[j] = new List<Card>();
                pcl[j].Add(remainingCards.Dequeue());
            }
        }
        
        int ind = 0;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_CARDS, pcl[ind].ToArray() }
            };
            player.SetCustomProperties(props);


            ind++;
        }
        return pcl;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (targetPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            DownloadMyCards();
        }
    }

    // Cada jugador sube las cartas de los montones después de hacer una jugada.
    public void SyncRoomCards()
    {
        Hashtable roomProps = new Hashtable
        {
            { OchoLoco.REMAINING_CARDS, remainingCards.ToArray()    },
            { OchoLoco.PLAYING_CARDS,   playingCardStack.ToArray()  },
            { OchoLoco.PALO_FORZADO,    paloForzado                 }
        };
        photonView.RPC("SyncRoomCardsRPC", RpcTarget.All, roomProps);
    }

    [PunRPC]
    public void SyncRoomCardsRPC(Hashtable newCards)
    {
        if (newCards.TryGetValue(OchoLoco.REMAINING_CARDS, out object _remainingCards))
        {
            var arr = (Card[])_remainingCards;
            remainingCards = new Queue<Card>(arr);
        }

        if (newCards.TryGetValue(OchoLoco.PLAYING_CARDS, out object _playingCards))
        {
            var newStack = new Stack<Card>(((Card[])_playingCards).Reverse());
            playingCardStack = newStack;
        }

        if (newCards.TryGetValue(OchoLoco.PALO_FORZADO, out object _palo))
        {
            paloForzado = (int)_palo;
        }

        RefreshStacks();
    }

    public void RefreshStacks() // llamado después de cada jugada
    {
        if (remainingCards.Count == 0)
        {
            var toKeep = playingCardStack.Pop(); // La carta que se está jugando
            remainingCards = new Queue<Card>(playingCardStack.Reverse()); // le damos la vuelta (barajamos también?)
            playingCardStack.Clear(); // vaciamos el playing stack
            playingCardStack.Push(toKeep); // y le añadimos solo la carta que se está jugando
        }

        // paloForzado = playingCardStack.Peek().palo;
        stealingStack.sprite = GetCardSprite("back_0_0");
        stealingStackText.text = remainingCards.Count.ToString();

        playingStack.sprite = GetCardSprite(playingCardStack.Peek());
    }

    public void DownloadMyCards() // Cada jugador llama a esto para obtener sus cartas al comienzo de cada turno.
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object _myCards))
        {
            var cs = (Card[])_myCards;
            MyCards = cs.ToList();
        }

        RefreshPlayerCardSelectors();
    }

    // Cada jugador sube sus cartas al final de cada turno. El Master Client las recoge y las guarda en las properties de la room.
    public void UploadMyCards()
    {
        Hashtable props = new Hashtable
        {
            { OchoLoco.PLAYER_CARDS, MyCards.ToArray() }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void RefreshPlayerCardSelectors()
    {
        if (playerCardSelectors == null)
        {
            playerCardSelectors = new Dictionary<int, PlayerCardSelector>();
            int c = 0;
            foreach (Transform child in pcsParent)
            {
                Destroy(child.gameObject);
            }
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                bool isMe = p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
                GameObject obj = Instantiate(isMe ? playerCardSelectorPrefab : otherPlayerSelectorPrefab);
                obj.transform.SetParent(pcsParent);
                obj.transform.localScale = Vector3.one;
                RectTransform rt = obj.GetComponent<RectTransform>();

                if (isMe)
                {
                    rt.anchoredPosition = new Vector2(0, -175);
                    rt.sizeDelta = new Vector2(450, 100);
                }
                else
                {
                    switch (c)
                    {
                        case 0:
                            rt.anchoredPosition = new Vector2(350, 0);
                            rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 90));
                            break;
                        case 1:
                            rt.anchoredPosition = new Vector2(-350, 0);
                            rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 270));
                            break;
                        case 2:
                            rt.anchoredPosition = new Vector2(0, 175);
                            rt.localRotation = Quaternion.Euler(new Vector3(0, 0, 180));
                            break;
                        default:
                            break;
                    }
                    c++;
                }

                var item = obj.GetComponent<PlayerCardSelector>();
                item.Initialize(p, p.ActorNumber, p.NickName, p.IsMasterClient);
                playerCardSelectors.Add(p.ActorNumber, item);
            }
        }
        else
        {
            foreach (var item in playerCardSelectors.Values)
            {
                item.Refresh();
            }
        }
    }

    public void Paso()
    {
        console.WriteLine("Paso!", PhotonNetwork.LocalPlayer.NickName);
        SendMove(null, true);
    }

    public void VoyPorUna()
    {
        if (MyCards.Count == 1)
        {
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_VP1, true }
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            console.WriteLine("Voy por una!", PhotonNetwork.LocalPlayer.NickName);
        }
    }

    public void PlayCard(Card c, Player p, int pForzado = -1)
    {
        if (CanPlayCard(c) || cheat) // si tu carta vale pasas
        {
            playingCardStack.Push(c);
            MyCards.Remove(c); // it should always have it
            if (pForzado != -1)
            {
                paloForzado = pForzado;
                console.WriteLine("El palo es " + palos[paloForzado]);
            }
            else
            {
                paloForzado = c.palo;
            }

            SendMove(c, true);
        }
    }

    public void OnRemainingStackClicked()
    {
        RefreshStacks(); // asegurarse de que el remaining stack se rellena

        if (!CanPlayAnyCard(true) && remainingCards.Count > 0) // solo robas si no puedes jugar ninguna
        {
            Robar(PhotonNetwork.LocalPlayer);
        }
    }

    public void AddCard(Player p, Card[] c)
    {
        if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL))
        {
            var l = ((Card[])cardL).ToList();
            l.AddRange(c);
            
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_CARDS, l.ToArray() },
                { OchoLoco.PLAYER_VP1, false }
            };
            p.SetCustomProperties(props);
            playerCardSelectors[p.ActorNumber].ScrollToCard(MyCards[MyCards.Count - 1]);
            SyncRoomCards();
        }
    }

    public void Robar(Player p, int amount=1, bool force=false)
    {
        if (!IsMyTurn(PhotonNetwork.LocalPlayer) && !force)
            return;
        List<Card> cs = new List<Card>();
        for (int i = 0; i < amount; i++)
        {
            cs.Add(remainingCards.Dequeue());
        }
        AddCard(p, cs.ToArray());
        var c = amount == 1 ? " carta." : " cartas.";
        console.WriteLine(p.NickName + " roba " + amount + c);
    }

    public bool CanPlayAnyCard(bool ignore8s)
    {
        foreach (Card c in MyCards)
        {
            if (ignore8s && c.num == 8)
                continue;
            if (CanPlayCard(c))
                return true;
        }
        return false;
    }

    public bool CanPlayCard(Card c) // palo forzado, 8s, cartas normales
    {
        if (!IsMyTurn(PhotonNetwork.LocalPlayer))
            return false;
        Card currentCard = playingCardStack.Peek();
        // Si la última carta jugada fue un 8, da igual el número que tires
        bool num = (currentCard.num == 8) ? true : c.num == currentCard.num;
        // Debug.Log("Palo? (" + palos[paloForzado] + ") " + (c.palo == paloForzado));
        // Debug.Log("Número? (" + currentCard.num + ") " + num);

        if (c.palo == paloForzado || num || c.num == 8)
        {
            return true;
        }
        return false;
    }

    public bool IsMyTurn(Player p)
    {
        int myIndex = PhotonNetwork.PlayerList.ToList().IndexOf(p);// + 1;
        return Turn % PhotonNetwork.PlayerList.Length == myIndex;
    }

    public void NoHasPasado(Player sender, Player target)
    {
        if (turnHistory.Count < Turn - 1) // TODO el primer turno es 0 o 1????
            return;

        // Debug.Log("Last card was " + playingCardStack.ElementAt(1).num + ", but " + turnHistory[Turn - 2].Key.NickName + " moved " + turnHistory[Turn - 2].Value.ToString());
        // Si la carta anterior era un 2, y has tirado algo, a robar
        if (playingCardStack.ElementAt(1).num == 2 && turnHistory[Turn - 2].Key.ActorNumber == target.ActorNumber && turnHistory[Turn - 2].Value != null)
        {
            console.WriteLine(target.NickName + " no ha pasado!\nA robar!", sender.NickName);
            Robar(target, pasarPenal, true);
        }
    }

    public void NoHasDichoVP1(Player sender, Player target)
    {
        if (target.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL)
            && target.CustomProperties.TryGetValue(OchoLoco.PLAYER_VP1, out object vp1))
        {
            var l = (Card[])cardL;
            // si tienes 1 carta y no has dicho voy por 1, robas
            if (l.Length == 1 && !(bool)vp1)
            {
                console.WriteLine(target.NickName + " no ha dicho voy por 1!\nA robar!", sender.NickName);
                Robar(target, vp1Penal, true);
            }
        }
    }

    public Card GetCardFromSprite(Sprite s)
    {
        GroupCollection digits = Regex.Match(s.name, @"(\d+)(\D)(\d+)").Groups;
        return new Card(int.Parse(digits[1].Value), int.Parse(digits[3].Value));
    }

    public Sprite GetCardSprite(Card c)
    {
        return GetCardSprite(string.Format("cards_{0}_{1}", c.palo, c.num));
    }

    public Sprite GetCardSprite(string sName)
    {
        return cardSprites.Where(x => x.name == sName).FirstOrDefault();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        throw new System.NotImplementedException();
    }

    public void OnTurnBegins(int turn)
    {
        var s = "";
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            playerCardSelectors[p.ActorNumber].ToggleTurnIndicator(IsMyTurn(p));
            if (IsMyTurn(p))
                s = p.NickName;
        }
        if (PhotonNetwork.IsMasterClient)
        {
            console.WriteLine("Turno " + turn + " (" + s + ")");
            Debug.Log("Sending turn beginning sequence" + PhotonNetwork.LocalPlayer.NickName);
        }
    }

    public void OnTurnCompleted(int turn) { return; }

    public void OnPlayerMove(Player player, int turn, object move) { return; }

    public void OnPlayerFinished(Player player, int turn, object move)
    {
        if (PhotonNetwork.IsMasterClient)
            console.WriteLine(move, player.NickName);
        if (player == PhotonNetwork.LocalPlayer)
        {
            UploadMyCards();
            SyncRoomCards();
        }

        turnHistory.Add(new KeyValuePair<Player, object>(player, move));

        if (player.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL))
        {
            var l = (Card[])cardL;
            // console.WriteLine("A " + player.NickName + " le quedan " + l.Length + " cartas"); TODO un número debajo del nombre del jugador
            if (l.Length == 1) // no se por que sale 1??
            {
                console.WriteLine(player.NickName + " gana la partida!!!");
                Win();
                return;
            }
        }

        if (!PhotonNetwork.IsMasterClient)
            return;

        BeginTurn();
    }

    public void Win()
    {
        gamePanel.SetActive(false);
        winPanel.SetActive(true);
        Dictionary<Player, int> playerCardCount = new Dictionary<Player, int>();

        foreach (Player p in PhotonNetwork.PlayerList)
        {
            if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object cardL))
            {
                playerCardCount.Add(p, ((Card[])cardL).Length);
                if (playerCardCount.Count >= 3)
                    break;
            }
        }

        var pList = playerCardCount.OrderBy(x => x.Value).ToList();
        for (int i = 0; i < podium.Length; i++)
        {
            string s = "";
            if (i < pList.Count)
                s = pList[i].Key.NickName;
            podium[i].SetText(s);
        }
    }

    public void PlayAgain()
    {
        photonView.RPC("RestartGameRPC", RpcTarget.All);
    }

    [PunRPC]
    public void RestartGameRPC()
    {
        StartCoroutine(StartGame());
    }

    private IEnumerator StartGame()
    {
        yield return new WaitForSeconds(0.2f);
        if (debug) Debug.Log("starting game");
        PhotonNetwork.LoadLevel(OchoLoco.GAME_SCENE_INDEX);
    }

    public void OnTurnTimeEnds(int turn)
    {
        return;
    }
}

    [System.Serializable]
public struct Card
{
    public int palo;
    public int num;

    public Card(int palo, int num)
    {
        this.palo = palo;
        this.num = num;
    }

    public static bool CompareCards(Card c1, Card c2)
    {
        return c1.num == c2.num || c1.palo == c2.palo;
    }

    public static bool operator ==(Card c1, Card c2)
    {
        return c1.Equals(c2);
    }

    public static bool operator !=(Card c1, Card c2)
    {
        return !c1.Equals(c2);
    }

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        var c = (Card)obj;
        return c.palo == palo && c.num == num;
    }

    public override string ToString()
    {
        return string.Format("{0} de {1}", num, CardManager.palos[palo]);
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static readonly byte[] memCard = new byte[2 * 4];
    public static short SerializeCard(StreamBuffer outStream, object customobject)
    {
        Card co = (Card)customobject;
        lock (memCard)
        {
            byte[] bytes = memCard;
            int index = 0;
            Protocol.Serialize(co.palo, bytes, ref index);
            Protocol.Serialize(co.num, bytes, ref index);
            outStream.Write(bytes, 0, 2 * 4);
        }

        return 2 * 4;
    }

    public static object DeserializeCard(StreamBuffer inStream, short length)
    {
        Card co = new Card();
        lock (memCard)
        {
            inStream.Read(memCard, 0, 2 * 4);
            int index = 0;
            Protocol.Deserialize(out co.palo, memCard, ref index);
            Protocol.Deserialize(out co.num, memCard, ref index);
        }

        return co;
    }
}
