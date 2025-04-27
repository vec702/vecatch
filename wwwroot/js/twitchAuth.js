window.extractTwitchToken = function () {
    let hash = window.location.hash;
    if (!hash) return "";
    let params = new URLSearchParams(hash.substring(1));
    return params.get("access_token") || "";
};
