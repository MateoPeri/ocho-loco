using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using ExitGames.Client.Photon;

public class CardManager : MonoBehaviourPunCallbacks
{
    public static CardManager Instance;

    public List<Sprite> cardSprites = new List<Sprite>();
    public List<Card> allCards;
    public Queue<Card> remainingCards; // cartas del montón (para robar)
    public Stack<Card> playingCardStack; // el montón con el que se juega

    List<Card>[] playerCardList; // una lista de listas para guardar las cartas de los jugadores

    public GameObject playerCardSelectorPrefab, otherPlayerSelectorPrefab;
    public Transform pcsParent;

    public Image stealingStack, playingStack;
    public TMP_Text stealingStackText;

    private Dictionary<int, PlayerCardSelector> playerCardSelectors;

    public int paloForzado = -1;

    private bool playerUpdated, roomUpdated;

    private List<Card> _myCards;
    public List<Card> MyCards
    {
        get { return _myCards; }
        set
        {
            _myCards = value;
            //OnMyCardsUpdated(); // esto en principio no es necesario si subimos las cartas al final de cada turno
            /*
            if (playerCardList == null)
                playerCardList = new List<List<Card>>();

            if (!playerCardList.Any())
                playerCardList = Enumerable.Repeat<List<Card>>(null, PhotonNetwork.CurrentRoom.PlayerCount).ToList();
            playerCardList.Insert(PhotonNetwork.LocalPlayer.ActorNumber, value);
            */
        }
    }

    public int nBarajas = 1;
    public bool isPlayerTurn;

    public bool debug;

    private void Awake()
    {
        Instance = this;
        PhotonPeer.RegisterType(typeof(Card), (byte)'C', Card.SerializeCard, Card.DeserializeCard);
    }

    private void Start()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            GroupCollection[] digits = cardSprites.Select(x => Regex.Match(x.name, @"(\d+)(\D)(\d+)").Groups).ToArray();
            allCards = new List<Card>();
            foreach (GroupCollection value in digits)
            {
                if (int.Parse(value[3].Value) == 0) // para las cartas especiales ej (cards_0_0)
                    continue;
                // Debug.Log(value[1].Value + ", " + value[3].Value); // 0 -> 0_12; 1 -> 0; 2 -> _; 3 -> 12
                allCards.Add(new Card(
                    int.Parse(value[1].Value),
                    int.Parse(value[3].Value)
                    ));
            }
            /*
            allCards = cardSprites.Select(x => new Card(
                int.Parse(x.name.Substring(6,7)), // cards_0_1
                int.Parse(x.name.Substring(8,9)))).ToList();
            */
            nBarajas = Mathf.Max(1, Mathf.CeilToInt(PhotonNetwork.CurrentRoom.PlayerCount / 4)); // 1 baraja por cada 4 jugadores --> 20 cartas libres

            remainingCards = new Queue<Card>(allCards);
            playingCardStack = new Stack<Card>();
            playerCardList = RepartirCartas(nBarajas);
            playingCardStack.Push(remainingCards.Dequeue()); // TODO: seguro que es dequeue??

            //UpdateRoomProps();
            UploadRoomCards();
        }
    }

    private void UpdateRoomProps() // El master client debería llamar a esto cada turno // que diferencia hay entre esto y UploadRoomCards??? la pclArr no se usa en ningún lado
    {
        // esto peta
        if (!PhotonNetwork.IsMasterClient)
            return;

        Card[][] pclArr = playerCardList.Select(x => x.ToArray()).ToArray();
        Hashtable roomProps = new Hashtable
        {
            { OchoLoco.ROOM_CARD_LIST,  pclArr                      },
            { OchoLoco.REMAINING_CARDS, remainingCards.ToArray()    },
            { OchoLoco.PLAYING_CARDS,   playingCardStack.ToArray()  }
        };
        roomUpdated = false;
        Debug.Log("uploading room settings (master version)...");
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
    }

    private void Update() // esto habría que cambiarlo por algo que solo se ejecutase cada turno
    {
        return;
        if (roomUpdated && playerUpdated)
        {
            roomUpdated = false;
            playerUpdated = false;
            DownloadMyCards();
        }

        return;
        UploadMyCards();
    }

    public void LeaveRoom() // mover a otro lado porque aquí no pinta nada
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        //UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public override void OnLeftRoom()
    {
        //PhotonNetwork.Disconnect();
        //UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        Debug.Log("Left room");
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
        // TODO: esto sería lo ideal. que hubiese una lista de cartas central y que los players la cachearan al principio de cada turno.
        /*
        Hashtable roomProps = new Hashtable
        {
            { OchoLoco.ROOM_CARD_LIST, pcl }
        };
        */
        
        int ind = 0;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable
            {
                { OchoLoco.PLAYER_CARDS, pcl[ind].ToArray() } // PUN no soporta listas!
            };
            player.SetCustomProperties(props);

            // las descargamos despues
            //if (player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            //    MyCards = pcl[ind];

            ind++;
        }

        /* 
        int cardsPerPlayer = Mathf.FloorToInt(PhotonNetwork.CurrentRoom.PlayerCount / nBarajas);
        Card[][] playerCards = new Card[PhotonNetwork.CurrentRoom.PlayerCount][];
        for (int i = 0; i < PhotonNetwork.CurrentRoom.PlayerCount; i++)
        {
            playerCards[i] = remainingCards
                                .Select(v => new { v, i = rnd.Next() })
                                .OrderBy(x => x.i).Take(cardsPerPlayer)
                                .Select(x => x.v).ToArray();
            foreach (Card c in playerCards[i])
                remainingCards.Remove(c);
        }
        */
        return pcl;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        playerUpdated = targetPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        if (targetPlayer.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            Debug.Log("downloading player cards due to player update");
            DownloadMyCards();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(OchoLoco.REMAINING_CARDS) || propertiesThatChanged.ContainsKey(OchoLoco.PLAYING_CARDS))
        {
            Debug.Log("downloading room cards due to room update");
            if (debug)
                foreach (DictionaryEntry kvp in propertiesThatChanged)
                    Debug.Log(string.Format("Key = {0}, Value = {1}", kvp.Key, kvp.Value));
            if (propertiesThatChanged.TryGetValue(OchoLoco.PLAYING_CARDS, out object pc))
            {
                Debug.Log("this is a test!");
                var arr = (Card[])pc;
                var test = playingCardStack.Peek();
                playingCardStack = new Stack<Card>(arr);
                Debug.Log(playingCardStack.Peek() + " | " + test);
            }
            DownloadRoomCards();
        }
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

        stealingStack.sprite = GetCardSprite("back_0_0");
        stealingStackText.text = remainingCards.Count.ToString();

        playingStack.sprite = GetCardSprite(playingCardStack.Peek());
    }

    public void DownloadRoomCards()
    {
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(OchoLoco.REMAINING_CARDS, out object _remainingCards))
        {
            var arr = (Card[])_remainingCards;
            remainingCards = new Queue<Card>(arr);
            Debug.Log("downloading remaining cards. null: " + (remainingCards == null));
        }
        else
            Debug.Log("remaining cards download failed");

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(OchoLoco.PLAYING_CARDS, out object _playingCards))
        {
            var arr = (Card[])_playingCards;
            var test = playingCardStack.Peek();
            playingCardStack = new Stack<Card>(arr);
            Debug.Log("downloading playing cards. null: " + (playingCardStack == null));
            Debug.Log(playingCardStack.Peek() + " | " + test);
        }
        else
            Debug.Log("playing cards download failed");

        RefreshStacks();
    }

    public void DownloadMyCards() // Cada jugador llama a esto para obtener sus cartas al comienzo de cada turno.
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(OchoLoco.PLAYER_CARDS, out object _myCards))
        {
            var cs = (Card[])_myCards;
            MyCards = cs.ToList();
            Debug.Log("downloading my cards. null: " + (MyCards == null));
        } else
            Debug.Log("my cards download failed");

        RefreshPlayerCardSelectors(); // BUG esto se está llamando antes que los downloads!!!
    }

    public void UploadMyCards() // Cada jugador sube sus cartas al final de cada turno. El Master Client las recoge y las guarda en las properties de la room.
    {
        //playerCardList[PhotonNetwork.PlayerList.ToList().IndexOf(PhotonNetwork.LocalPlayer)] = myCards;
        Hashtable props = new Hashtable
        {
            {OchoLoco.PLAYER_CARDS, MyCards.ToArray()} // PUN no soporta listas!
        };
        Debug.Log("uploading my cards");
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    public void UploadRoomCards() // Cada jugador sube las cartas de los montones después de hacer una jugada.
    {
        Hashtable roomProps = new Hashtable
        {
            { OchoLoco.REMAINING_CARDS, remainingCards.ToArray()    },
            { OchoLoco.PLAYING_CARDS,   playingCardStack.ToArray()  }
        };
        //roomUpdated = false;
        Debug.Log("uploading room cards...");
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
    }

    private void RefreshPlayerCardSelectors()
    {
        //Debug.Log("null check " + (MyCards == null));
        playerCardSelectors = new Dictionary<int, PlayerCardSelector>();
        int c = 0;
        foreach (Transform child in pcsParent)
        {
            Destroy(child.gameObject); // destruir cada vez que hay que refrescar es muy bestia
        }
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            bool isMe = p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
            GameObject obj = Instantiate((isMe) ? playerCardSelectorPrefab : otherPlayerSelectorPrefab);
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
            item.Initialize(p.ActorNumber, p.NickName, p.IsMasterClient);

            /*
            if (p.CustomProperties.TryGetValue(OchoLoco.PLAYER_READY, out object isPlayerReady))
            {
                item.SetPlayerTurn(isPlayerTurn);
            }
            */
            playerCardSelectors.Add(p.ActorNumber, item);
        }
    }

    public void PlayCard(Card c, Player p, int pForzado = -1, bool debug = false)
    {
        if (CanPlayCard(c) || debug) // TODO debug // si tu carta vale o si tienes un ocho pasas
        {
            playingCardStack.Push(c);
            MyCards.Remove(c); // it should always have it
            paloForzado = pForzado;

            UploadMyCards();
            // we call refresh once the cards reach the server and are downloaded back RefreshPlayerCardSelectors(); // refresh cards on playercardselector
            UploadRoomCards();
            // we call refresh once the cards reach the server and are downloaded back RefreshStacks(); // refresh card stacks
        }
    }

    public void OnRemainingStackClicked()
    {
        RefreshStacks(); // asegurarse de que el remaining stack se rellena

        if ((!CanPlayAnyCard(true) && remainingCards.Count > 0) || true) // TODO: quitar el debug // solo robas si no puedes jugar ninguna
        {
            Robar(PhotonNetwork.LocalPlayer);
        }
    }

    public void Robar(Player p) // TODO: player???? mycards están vinculadas al player ya no??
    {
        MyCards.Add(remainingCards.Dequeue());
        UploadMyCards();
        // we call refresh once the cards reach the server and are downloaded back RefreshPlayerCardSelectors();
        //playerCardSelectors[PhotonNetwork.LocalPlayer.ActorNumber].ScrollTo(myCards.Count - 1); // TODO: ese index no lo veo yo muy claro
        playerCardSelectors[PhotonNetwork.LocalPlayer.ActorNumber].ScrollToCard(MyCards[MyCards.Count - 1]);
        UploadRoomCards();
        // we call refresh once the cards reach the server and are downloaded back RefreshStacks();
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
        Card currentCard = playingCardStack.Peek();

        if (paloForzado != -1) // si hay un palo forzado, tienes q tirar ese palo o un 8
        {
            if (c.palo == paloForzado || c.num == 8)
            {
                return true;
            }
        }
        else
        {
            if (Card.CompareCards(c, currentCard) || c.num == 8) // si tu carta vale o si tienes un ocho pasas
            {
                return true;
            }
        }

        return false;
    }

    public Sprite GetCardSprite(Card c)
    {
        return GetCardSprite(string.Format("cards_{0}_{1}", c.palo, c.num));
    }

    public Sprite GetCardSprite(string sName)
    {
        return cardSprites.Where(x => x.name == sName).FirstOrDefault();
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
        return string.Format("Card(Palo={0}, Num={1})", palo, num);
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
