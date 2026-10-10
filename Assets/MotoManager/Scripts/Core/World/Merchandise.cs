using System;
using System.Collections.Generic;

namespace MotoManager.Core.World
{
    public enum ProductIdentity
    {
        Team,
        /// <summary>Manufacturer branding: only works teams may sell it, and they pay the manufacturer a royalty.</summary>
        Manufacturer,
        /// <summary>A rider's name or face: needs image rights in the rider's contract; the rider gets a share.</summary>
        Rider,
        /// <summary>Co-branded with one of the team's main sponsors, who pays part of the production.</summary>
        Sponsor
    }

    /// <summary>Who buys it: casual fans react strongly to price, collectors much less.</summary>
    public enum ProductSegment
    {
        Casual,
        Fan,
        Collector
    }

    /// <summary>Products that only make sense after something happened in the game.</summary>
    public enum ProductRequirement
    {
        None,
        RiderRights,
        Win,
        Title
    }

    /// <summary>A product that can be sold in team shops (data: merchandise/*.json). New products need no code.</summary>
    [Serializable]
    public class ProductTemplate
    {
        public string Id;
        /// <summary>Localization key suffix ("product." + Type) and grouping (T-shirt, Cap, ...).</summary>
        public string Type;
        public ProductIdentity Identity = ProductIdentity.Team;
        public ProductSegment Segment = ProductSegment.Casual;
        /// <summary>Unit cost of making or buying it (before volume discounts).</summary>
        public double BaseUnitCost = 10;
        /// <summary>The price fans consider normal.</summary>
        public double ReferencePrice = 30;
        /// <summary>Relative demand of the product at its reference price (1 = a T-shirt).</summary>
        public double Appeal = 1;
        public int MinOrder = 100;
        public ProductRequirement Requires = ProductRequirement.None;
        /// <summary>Special editions: events it stays on sale after the result that created it (0 = the whole season).</summary>
        public int EditionEvents;
        public string DataConfidence;
    }

    /// <summary>One product in a team's shop: price, stock (valued at average cost) and sales.</summary>
    [Serializable]
    public class MerchItem
    {
        public string Id;
        public string TemplateId;
        public string RiderId;
        public string SponsorId;
        public double Price;
        public bool Active = true;
        public int Stock;
        /// <summary>Cost of the units in stock (average cost method).</summary>
        public double StockValue;
        public int SoldSeason;
        public int LostSalesSeason;
        public double RevenueSeason;
        public double CostOfSalesSeason;
        public int SoldTotal;
        public double RevenueTotal;
        public double CostOfSalesTotal;
        /// <summary>Units sold at each event of the current season (for the sales chart).</summary>
        public List<int> SalesByEvent = new List<int>();

        public double AverageUnitCost { get { return Stock > 0 ? StockValue / Stock : 0; } }
    }

    /// <summary>A team's official shop and its fan base.</summary>
    [Serializable]
    public class TeamShop
    {
        public List<MerchItem> Items = new List<MerchItem>();
        /// <summary>Size of the team's fan base (people who follow it).</summary>
        public double Fans;
        /// <summary>Temporary extra interest after wins and titles (fades event by event).</summary>
        public double Hype;
        /// <summary>Events since the team's last win (-1 = no win yet): special win editions sell for a while after it.</summary>
        public int EventsSinceWin = -1;
        public List<MerchCampaign> Campaigns = new List<MerchCampaign>();
    }

    public enum CampaignType
    {
        /// <summary>Social media push: cheap, short.</summary>
        Social,
        /// <summary>Fan event at the circuit: brings fans, short.</summary>
        FanEvent,
        /// <summary>Products in a retail chain: expensive, long.</summary>
        RetailPartner
    }

    /// <summary>A marketing campaign running for the shop.</summary>
    [Serializable]
    public class MerchCampaign
    {
        public CampaignType Type;
        public double Cost;
        /// <summary>Extra demand while it runs (0.3 = +30%).</summary>
        public double Boost;
        public int EventsLeft;
    }
}
