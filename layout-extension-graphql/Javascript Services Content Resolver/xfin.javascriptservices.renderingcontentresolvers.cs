using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Sitecore.JavaScriptServices.GraphQL.LayoutService;
using Sitecore.Services.GraphQL.Hosting;
using Sitecore.JavaScriptServices.GraphQL.Helpers;
using Sitecore.LayoutService.ItemRendering.ContentsResolvers;
using Sitecore.JavaScriptServices.Configuration;
using Sitecore.Services.GraphQL.Abstractions;
using Sitecore.Services.GraphQL.Hosting.Configuration;
using Sitecore.Services.GraphQL.Hosting.Performance;
using Sitecore.Services.GraphQL.Hosting.QueryTransformation;
using Sitecore.LayoutService.Configuration;
using Sitecore.Mvc.Presentation;
using Sitecore.Data.Items;
using System.Collections.Generic;
using Sitecore;
using System;
using System.Web;
using System.Collections.Specialized;

namespace layout_extension_graphql.Javascript_Services_Content_Resolver
{
    public class XfinAIQResolver : RenderingContentsResolver
    {
        public const string PlaceholderPathKey = "PlaceholderPath";

        public override object ResolveContents(Rendering rendering, IRenderingConfiguration renderingConfig)
        {
            //TODO Replace with correct path - Example of getting rendering field items to get path of card ID's
            string getCardPath = rendering.Item.Fields["sample1"].ToString();

            var cardsToSerialize = new cardItems();
            List<aiqClass> cardList = new List<aiqClass>();
            try
            {
                //TODO null check
                string cardIDList = HttpContext.Current.Request.QueryString["cardIDList"];
                string[] cardIDs = cardIDList.Split(',');
                foreach (var cardID in cardIDs)
                {
                    var item = Sitecore.Context.Database.GetItem(getCardPath + cardID);
                    if (item != null)
                    {
                        var associatedItem = Sitecore.Context.Database.GetItem(item["AssociatedItem"]);
                        if (associatedItem != null)
                        {
                            try
                            {
                                //TODO get subtext, mobile image, etc.
                                //var layoutInfo = Sitecore.Context.Database.GetItem(associatedItem["Layout"]);
                                Sitecore.Data.Fields.LinkField desktopImage = associatedItem.Fields["DesktopImage"];
                                aiqClass aiqItem = new aiqClass();
                                aiqItem.cardId = item["CardID"];
                                aiqItem.heading = associatedItem["Heading"];
                                aiqItem.desktopImage = desktopImage.Value;
                                aiqItem.layout = associatedItem["Layout"];
                                cardList.Add(aiqItem);
                            }
                            catch (Exception ex)
                            {
                                //log error
                                //do nothing
                            }
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                //Log error
                return null;
            }
            JObject json = new JObject();
            json["cards"] = JToken.FromObject(cardList);
            json[PlaceholderPathKey] = rendering.Placeholder;
            return json;
        }
    }
}