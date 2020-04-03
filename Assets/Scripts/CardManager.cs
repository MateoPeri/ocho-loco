using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using System;
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
    public List<Transform> cardParents = new List<Transform>();

    public GameObject playerCardSelectorPrefab, otherPlayerSelectorPrefab;
    public Transform pcsParent;

    private Dictionary<int, PlayerCardSelector> playerCardSelectors;

    public int paloForzado = -1;

    public List<Card> myCards = new List<Card>();

    public int nBarajas = 1;
    public bool isPlayerTurn;

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
            RepartirCartas(nBarajas);

            RefreshPlayerCardSelectors();
        }
    }

    public void LeaveRoom() // mover a otro lado porque aquí no pinta nada
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    public override void OnLeftRoom()
    {
        //PhotonNetwork.Disconnect();
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);

    }

    public void RepartirCartas(int n = 1)
    {
        var rnd = new System.Random();
        remainingCards = new Queue<Card>(remainingCards.ToList().Shuffle());

        List<Card>[] playerCardList = new List<Card>[PhotonNetwork.CurrentRoom.PlayerCount];

        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < playerCardList.Length; j++)
            {
                if (playerCardList[j] == null)
                    playerCardList[j] = new List<Card>();
                playerCardList[j].Add(remainingCards.Dequeue());
            }
        }

        int ind = 0;
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            Hashtable props = new Hashtable
            {
                {OchoLoco.PLAYER_CARDS, playerCardList[ind].ToArray()} // PUN no soporta listas!
            };
            player.SetCustomProperties(props);

            if (player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
                myCards = playerCardList[ind];
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
    }

    private void RefreshPlayerCardSelectors()
    {
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

            Debug.Log("rendering player " + c + ", is me? " + isMe);

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

    public bool TryPlayCard(Card c, Player p, int pForzado = -1)
    {
        Card currentCard = playingCardStack.Peek();

        if (paloForzado != -1 && c.palo == paloForzado) // si hay un palo forzado, tienes q entregar ese palo
        {
            if (Card.CompareCards(c, currentCard) || c.num == 8) // si tu carta vale o si tienes un ocho pasas
            {
                playingCardStack.Push(c);
                paloForzado = pForzado;
                // call OnCardPlayed async
                var _ = Task.Run(() => OnCardPlayed(p)); // funciona?
                return true;
            }
        }
        return false;
    }

    public void OnCardPlayed(Player p)
    {
        Card playedCard = playingCardStack.Peek();
        Debug.Log("OnCardPlayed");
        // refresh cards on playercardselector
    }

    public Card Robar()
    {
        return remainingCards.Dequeue();
    }

    public Sprite GetCardSprite(Card c)
    {
        return GetCardSprite(string.Format("cards_{0}_{1}", c.palo, c.num));
    }

    public Sprite GetCardSprite(string sName)
    {
        return cardSprites.Where(x => x.name == sName).FirstOrDefault(); ;
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
