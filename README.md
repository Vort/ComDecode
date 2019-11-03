With this software it is possible to receive data from serial port via line-in audio input of sound card.

For operation it requires simple hardware, which consists of several diodes and resistor.

## Hardware

Serial port uses voltages, which are too high for sound card inputs (-11 V and +11 V).

To fix this problem diodes can be used:

![](https://github.com/Vort/ComDecode/blob/master/com_schematic.png)

## Parameters

Since I have tested this software only on my PC, parameters of this program are changed directly in source code.

The things, which may need to be changed while using this software, are:

`sampleRate`: sample rate of recorded audio;

`baudRate`: baud rate of serial port;

`baudRateCorrection`: actual baud rate, divided by theoretical baud rate;

`detect_level`: received signal level;

`recoverFilter`: filter for removing of capacitive distortion of the signal.

## Test data

This project includes test file, [signal.wav](https://github.com/Vort/ComDecode/blob/master/signal.wav), which consists of 1.2 seconds of [ReactOS](https://github.com/reactos/reactos) logs, recorded during bootup. Sample rate is 192000 Hz, baud rate is 115200 Bd.

To check if receive works correctly, [prbs15.bin](https://github.com/Vort/ComDecode/blob/master/prbs15.bin) file is included. When bits of this file are correctly decoded, program will show a message.