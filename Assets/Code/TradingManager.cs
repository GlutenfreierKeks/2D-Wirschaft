using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

public class TradingManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    public static TradingManager Instance;

    private const string OffersPropertyKey = "TradeOffers";
    private const byte GoldTransferEventCode = 2; // For sending gold to the seller

    public delegate void OnOffersUpdatedDelegate();
    public event OnOffersUpdatedDelegate OnOffersUpdated;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.AddCallbackTarget(this);
    }

    public override void OnDisable()
    {
        base.OnDisable();
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public List<TradingOffer> GetActiveOffers()
    {
        if (!PhotonNetwork.InRoom) return new List<TradingOffer>();

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(OffersPropertyKey, out object offersJsonObj))
        {
            string json = (string)offersJsonObj;
            if (!string.IsNullOrEmpty(json))
            {
                TradingOfferList list = JsonUtility.FromJson<TradingOfferList>(json);
                if (list != null && list.offers != null)
                {
                    return new List<TradingOffer>(list.offers);
                }
            }
        }
        return new List<TradingOffer>();
    }

    private void SaveOffers(List<TradingOffer> offers)
    {
        if (!PhotonNetwork.InRoom) return;

        TradingOfferList list = new TradingOfferList { offers = offers.ToArray() };
        string json = JsonUtility.ToJson(list);

        Hashtable props = new Hashtable
        {
            { OffersPropertyKey, json }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    public bool CreateOffer(string resourceType, int amount, int totalPrice)
    {
        // Check if player has the resource
        if (!ResourceManager.Instance.HasResource(resourceType, amount))
        {
            Debug.LogWarning("Not enough resources to create offer.");
            return false;
        }

        // Remove resource immediately
        ResourceManager.Instance.SpendResource(resourceType, amount);

        List<TradingOffer> offers = GetActiveOffers();
        TradingOffer newOffer = new TradingOffer
        {
            offerId = System.Guid.NewGuid().ToString(),
            sellerActorNumber = PhotonNetwork.LocalPlayer.ActorNumber,
            sellerName = string.IsNullOrEmpty(PhotonNetwork.NickName) ? "Spieler" : PhotonNetwork.NickName,
            resourceType = resourceType,
            amount = amount,
            totalPrice = totalPrice,
            timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        offers.Add(newOffer);
        SaveOffers(offers);
        return true;
    }

    public void CancelOffer(string offerId)
    {
        List<TradingOffer> offers = GetActiveOffers();
        TradingOffer offerToCancel = offers.Find(o => o.offerId == offerId);

        if (offerToCancel != null && offerToCancel.sellerActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            offers.Remove(offerToCancel);
            SaveOffers(offers);

            // Refund resource
            ResourceManager.Instance.AddResource(offerToCancel.resourceType, offerToCancel.amount);
        }
    }

    public void BuyOffer(string offerId)
    {
        List<TradingOffer> offers = GetActiveOffers();
        TradingOffer offerToBuy = offers.Find(o => o.offerId == offerId);

        if (offerToBuy == null) return;

        // Check if player has enough gold
        if (!ResourceManager.Instance.HasResource("gold", offerToBuy.totalPrice))
        {
            Debug.LogWarning("Not enough gold to buy offer.");
            return;
        }

        // Deduct gold and add resource
        ResourceManager.Instance.SpendResource("gold", offerToBuy.totalPrice);
        ResourceManager.Instance.AddResource(offerToBuy.resourceType, offerToBuy.amount);

        // Remove the offer
        offers.Remove(offerToBuy);
        SaveOffers(offers);

        // Send gold to the seller
        object[] content = new object[] { offerToBuy.totalPrice };
        RaiseEventOptions options = new RaiseEventOptions { TargetActors = new int[] { offerToBuy.sellerActorNumber } };
        SendOptions sendOptions = new SendOptions { Reliability = true };
        PhotonNetwork.RaiseEvent(GoldTransferEventCode, content, options, sendOptions);
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(OffersPropertyKey))
        {
            OnOffersUpdated?.Invoke();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Only MasterClient should clean up to avoid multiple updates
        if (PhotonNetwork.IsMasterClient)
        {
            List<TradingOffer> offers = GetActiveOffers();
            int originalCount = offers.Count;
            offers.RemoveAll(o => o.sellerActorNumber == otherPlayer.ActorNumber);

            if (offers.Count < originalCount)
            {
                SaveOffers(offers);
            }
        }
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == GoldTransferEventCode)
        {
            object[] data = (object[])photonEvent.CustomData;
            int goldAmount = (int)data[0];

            // Add gold to inventory
            ResourceManager.Instance.AddResource("gold", goldAmount);
            
            // Optional: Notification that an item was sold
            Debug.Log($"Item sold! You received {goldAmount} gold.");
        }
    }
}
