window.customSpeechSynthesis = {
    speakText: function (text) {
        if ('speechSynthesis' in window) {
            var utterance = new SpeechSynthesisUtterance(text);
            window.speechSynthesis.speak(utterance);
        } else {
            console.error('Speech synthesis not supported in this browser.');
        }
    },
    playAudio: function (audioUrl) {
        const audio = new Audio(audioUrl);
        audio.play();
    }
};
