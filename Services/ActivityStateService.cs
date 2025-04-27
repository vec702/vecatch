using System;
using System.Collections.Generic;
using VeCatch.Models;

namespace VeCatch.Services
{
    public class ActivityStateService
    {
        public List<Chatter> ActiveChatters { get; set; } = new List<Chatter>();
        public Dictionary<string, DateTime> ChatterActivity { get; set; } = new Dictionary<string, DateTime>();
    }

}