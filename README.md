# Myndband-Recorder
![Myndband Recorder Interface](https://github.com/med-material/Myndband-Recorder/blob/master/screenshot.PNG)

Application written with Unity to interface with the ThinkGear SDK, reading Raw EEG at 512Hz and myndband data at 1hz.
The Recorder records all activity to memory, and when recording is stopped, saves the recording to a CSV file.

Requires running the ThinkGear Connector ([Download Here](http://neurosky.fetchapp.com/permalink/a382ab)) in the background.

 * Reads eegPower (delta, theta, alpha, beta, gamma) from the Myndband headset and provides the data at variable frequency to Unity (by default every 0.5ms).
 * Can record data at 512hz for a period of time and save the recorded data to CSV (Note: this is not using the protocol's built-in recorder).
