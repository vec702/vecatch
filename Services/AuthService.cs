namespace VeCatch.Services
{
    public class AuthService
    {
        private string? _accessToken;
        private string? _channelName;
        public void SetAccessToken(string token) => _accessToken = token;
        public string GetAccessToken()
        {
            return _accessToken?.Length > 0 ? _accessToken : string.Empty;
        }

        public void SetChannelName(string channelName) => _channelName = channelName;
        public string GetChannelName()
        {
            return _channelName?.Length > 0 ? _channelName : string.Empty;
        }
    }
}
