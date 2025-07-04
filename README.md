[![magicstick-logo](docs/magicstick-logo.png)](https://github.com/samartzidis/magicstick.io)
###### Apple Keyboard USB Adapter for PC

# About

**magicstick.io** is an absolutely zero-hassle, USB adapter for connecting Apple keyboards (Magic 1, Magic 2 or the older Apple Wireless) to PCs, game consoles, smart TVs, etc. providing the correct keymap translation.

That is, you get a working _Delete_, _Ctrl_, _Page Up/Down_, _Print Screen_, Multimedia keys etc. You get dual connection modes, wired and Bluetooth.

You also get key programmability. You can remap keys to perform multimedia functions, type Unicode/Emojis and more.

All modern versions of Windows (since Windows 95 OSR2) and Linux are supported. Additionally, any device that accepts conventional USB keyboards should work with it, such as game consoles or smart TVs.

#### magicstick.io USB dongle:
<a href="docs/1.png"><img src="docs/1.png" width="160" title="Clear version" /></a>
<a href="docs/2.png"><img src="docs/2.png" width="160" title="Clear version"/></a>
<a href="docs/4.png"><img src="docs/4.png" width="160" title="Clear version"/></a>
<a href="docs/3.png"><img src="docs/3.png" width="160" title="Wired and Wireless Connections"></a>
<a href="docs/bt-connection-time.gif"><img src="docs/bt-connection-time.gif" width="160" title="Bluetooth connection delay from power on"></a>

#### Optional magicstick-ui utility:
<a href="docs/20230927213111.png"><img src="docs/20230927213111.png" width="100" title="Settings"/></a>
<a href="docs/Untitled-1.png"><img src="docs/Untitled-1.png" width="100" title="Battery Indicator"/></a>
        
**magicstick.io** started as a hobby. I wanted to use my Apple keyboard on Windows but without developing a dedicated Windows kernel-mode driver, especially given how difficult this is with the latest Windows kernel-mode driver signing restrictions (e.g. see my [WinAppleKey](https://github.com/samartzidis/WinAppleKey) project). 

## How to get a magicstick.io Device

You can purchase a hand-made plug-and-play dongle from ![ebay](docs/ebay.png).</br>

- [USB Type-A Version](https://www.ebay.co.uk/itm/316085733600) (for older Apple keyboards).
- [USB-C Version](https://www.ebay.co.uk/itm/317035973932) (for newer Apple keyboards).

Each item sold supports the _BBC Children in Need_ [![charity](docs/ribbon.gif)](https://charity.ebay.co.uk/charity/i/BBC-Children-in-Need/11641).


## User Manual

The user manual is [here](docs/README.md).

## Release Notes and Support

The firmware release notes and relevant support file links are available [here](release-notes.md).

## Supported Apple Keyboard Models

| Model | Status |
| -------- | ------- |
| A1314 | Old keyboard. Supported but no UI battery level indicator in magicstick-ui. |
| A1644, A1843, A2450, A3203| Fully supported. |
| A2449, A3118, A3119, A2520 | Supported - but without fingerprint sensor functionality (yet). |

## Features

- magicstick.io is powered by a **133MHz dual-core Arm Cortex M0+** processor. All processing logic is implemented in **optimized C/C++ and assembly** code and is utilizing both processor cores. The dual USB stack is managed by the first core and the Bluetooth stack is managed by the second core.
- magicstick.io can function **both wired and wirelessly**. You can connect your Apple keyboard either via a standard USB to Lightning cable or wirelessly via Bluetooth. 
- You can freely switch between wired or wireless connection modes at any time.
- Wired and Bluetooth operation modes provide **surprisingly fast response times**. Tests performed with online measurement tools could not detect any extra delays over the default 16ms rebounce delay of a A1644 keyboard.

  ![](docs/20231001222021.png)
  
  _The above measurement was done on [clickspeedtester.com](https://www.clickspeedtester.com/keyboard-latency-test/) using an A1644 keyboard. It averaged the same between (all) MagicStick-wired, MagicStick-Bluetooth, and direct PC USB (that is without MagicStick and with no extra Windows drivers installed)_.
- magicstick.io is a microcontroller-based device so it **works immediately** as soon as it is powered on. This allows you to use the keyboard as early as at the PC boot process, e.g. for accessing the BIOS/UEFI menus. Also since there is no Operating System driver required, the keyboard just works correctly in BIOS/UEFI mode.
- **Programmable**. magicstick.io incorporates a user programmable key rules engine that allows you to directly **map** keys or key combinations to **custom multimedia** functions or to **Unicode** characters (**μ**) and **Emojis** (👍).
- magicstick.io supports an OS **battery level indicator** in both wired and wireless connection modes in both **Windows** and **Linux**. Ubuntu Linux natively supports a battery-level indicator whereas for Windows you can use the [magicstick-ui](docs#the-magicstickui-utility) utility.
- magicstick.io is built with **security** in mind. Its HID interface is open (see magicstick-ui GitHub source code) and locked down to a standard keyboard HID API on the side that connects to the PC plus a few extra reports for monitoring the battery level and configuring keys. The Bluetooth connection has Level 2 security enabled (wireless encryption). Additionally, Bluetooth can be completely disabled if needed via settings.
- In contrast to other similar solutions (e.g. MagicUtilities), magicstick.io has **no subscription fees** or any connected device restrictions. You own the device, and you can connect it to **as many keyboards or computers** as you like.
- **PC sleep/wake-up** is supported and works **in both wired and Bluetooth** connection modes (in contrast to pure software solutions such as MagicUtilities). The Bluetooth wake-up support is particularly useful for media centre PCs that you would normally want to wake up from a distance when hitting a key on the keyboard. 
- **Firmware updates**. Any future improvements, such as support for new keyboard models are easy to install and are provided for free.

## Compliance and Safety

The magicstick.io hardware is based on a programmed Raspberry Pi Pico W microcontroller. Please refer to this official link for details on [compliance and safety approvals](https://pip.raspberrypi.com/categories/688).

## Disclaimer

magicstick.io was professionally developed with <3 and attention to detail, following software engineering best practices. There is no 100% guarantee however that it will work for your particular setup neither I accept responsibility for anything going wrong to your equipment (including explosions, earthquakes and floods) or to you directly or indirectly through its use. By accepting to use the device and related software you also accept full responsibility for all of the above. 


 







