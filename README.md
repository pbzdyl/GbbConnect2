
# GbbConnect2

Program to connect inverters (eg: Deye) and program [GbbOptimizer.gbbsoft.pl.](https://GbbOptimizer.gbbsoft.pl/)

To connect with inverters program uses SolarmanV5 protocol. (Loggers serial numers: 17xxxxxxx, 21xxxxxxx or 40xxxxxxx) (maybe also: 27xxxxxxx, 23xxxxxxx)

To connect with GbbOptimizer program uses Mqtt server and own protocol.

GbbConnect remarks:
- Data on disk are grouped using number in column "No". So if you want start new inverter with new data then put new No.

## Download

Last version download: [GbbConnect2.msi](http://www.gbbsoft.pl/!download/GbbConnect2/GbbConnect2Setup.msi) [setup.exe](http://www.gbbsoft.pl/!download/GbbConnect2/setup.exe)

## Connection to inverter

if first connection failed than program tries to connect every 5 minutes.

## Connection to mqtt

If first connection to mqtt failed then program tries to connect every 5 minutes.

Program every minute sends keepalive messave to mqtt. If connected has been lost then every minute program tries to reconect.

## Setup in [GbbOptimizer](https://GbbOptimizer.gbbsoft.pl/)

Manual how setup GbbConnect2 with Deye and GbbVictronWeb: [Manual](https://gbboptimizer.gbbsoft.pl/Manual?Filters.PageNo=31)

## History

v1.0 - start version

# GbbConnect2Console

Program on console.

## Download

Last version download: [GbbConnect2Console.zip](http://www.gbbsoft.pl/!download/GbbConnect2/GbbConnect2Console.zip)

## Parameters

-? - list of parameters

--dont-wait-for-key -  don't wait for key, but just wait forever

# Docker

Program GbbConnect2Console can be run in docker. File Dockerfile is present in root directory.

## Configuration file

You can use GbbConnect program to create (and test) configuration file (My Documents\GbbConnect2\Parameters.xml). Then file can be move as /root/GbbConnect2/Parameters.xml on docker container.

## How to run on Docker

- Install docker (or DockerDesktop)
- Clone GbbConnect2: git clone https://github.com/gbbsoft/GbbConnect2
- enter to GbbConnect2 directory: cd GbbConnect2
- run from GbbConnect2 directory: docker build . -t gbbconnect2image
- create container: docker container create -i -t --name gbbconnect2console gbbconnect2image
- copy to current directory file Parameters.xml (or create based on exmaple below)
- copy Parameters.xml: docker cp ./Parameters.xml gbbconnect2console:/root/GbbConnect2/Parameters.xml
- start container: docker start gbbconnect2console
- make container always running: docker update --restart unless-stopped gbbconnect2console

## Sample Parameters.xml file

```
<?xml version="1.0" encoding="utf-8"?>
<Parameters Version="1" GbbVictronWeb_Mqtt_Address="gbboptimizer2.gbbsoft.pl" GbbVictronWeb_Mqtt_Port="8883" Server_AutoStart="1" IsVerboseLog="1" IsDriverLog="0" IsDriverLog2="0">
  <Plant Version="1" Number="1" Name="MyPlant" IsDisabled="0" AddressIP="<Deye dongle ip address>" PortNo="8899" SerialNumber=<Deye dongle SN>" GbbVictronWeb_PlantId="<your PlantId>" GbbVictronWeb_PlantToken="<Your Token>" />
</Parameters>
```
