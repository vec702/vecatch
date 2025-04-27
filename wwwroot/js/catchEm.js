window.saveTimers = function (settings) {
    localStorage.setItem("adminTimers", JSON.stringify(settings));
};

window.loadTimers = function () {
    const timers = localStorage.getItem("adminTimers");
    return timers ? timers : {};
};

window.PlayAudio = function (elementName) {
    var audioElement = document.getElementById(elementName);
    if (audioElement) {
        audioElement.play();
    } else {
        console.error(`Audio element with id '${elementName}' not found.`);
    }
};
