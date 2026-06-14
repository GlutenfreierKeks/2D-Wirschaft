using System;

[Serializable]
public class TradingOffer
{
    public string offerId;
    public int sellerActorNumber;
    public string sellerName;
    public string resourceType;
    public int amount;
    public int totalPrice;
    public long timestamp; // For sorting if prices are equal
}

[Serializable]
public class TradingOfferList
{
    public TradingOffer[] offers;
}
