@echo off
cd %~dp0

: python依赖包安装路径，安装包来源： pypi.org ================================ Start

: future安装包的路径(相对bat文件)
set futurePath=Dependencies\future-0.16.0.tar.gz

: pefile安装包的路径(相对bat文件)
set pefilePath=Dependencies\pefile-2017.11.5.tar.gz

: altgraph安装包的路径(相对bat文件)
set altgraphPath=Dependencies\altgraph-0.15.tar.gz

: maconlib安装包的路径(相对bat文件)
set maconlibPath=Dependencies\macholib-1.9.tar.gz

: PyInstaller安装包的路径(相对bat文件)
set pyInstallerPath=Dependencies\PyInstaller-3.3.1.tar.gz

: python依赖包安装路径，安装包来源： pypi.org ================================ End

pip install %~dp0%futurePath%
pip install %~dp0%pefilePath%
pip install %~dp0%altgraphPath%
pip install %~dp0%maconlibPath%
pip install %~dp0%pyInstallerPath%
pause