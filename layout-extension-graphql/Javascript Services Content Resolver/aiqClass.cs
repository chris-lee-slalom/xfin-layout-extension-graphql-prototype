using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace layout_extension_graphql.Javascript_Services_Content_Resolver
{
    public class aiqClass
    {
        public string cardId { get; set; }
        public string heading { get; set; }
        public string desktopImage { get; set; }
        public string mobileImage { get; set; }
        public string layout { get; set; }
        public string theme { get; set; }
    }
    public class cardItems
    {
        public List<aiqClass> items { get; set; }
    }
}