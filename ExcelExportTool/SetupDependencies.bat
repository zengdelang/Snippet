@echo off
cd %~dp0

: python��������װ·������װ����Դ�� pypi.org ================================ Start

: future��װ����·��(���bat�ļ�)
set futurePath=Dependencies\future-0.16.0.tar.gz

: pefile��װ����·��(���bat�ļ�)
set pefilePath=Dependencies\pefile-2017.11.5.tar.gz

: altgraph��װ����·��(���bat�ļ�)
set altgraphPath=Dependencies\altgraph-0.15.tar.gz

: maconlib��װ����·��(���bat�ļ�)
set maconlibPath=Dependencies\macholib-1.9.tar.gz

: PyInstaller��װ����·��(���bat�ļ�)
set pyInstallerPath=Dependencies\PyInstaller-3.3.1.tar.gz

: python��������װ·������װ����Դ�� pypi.org ================================ End

pip install %~dp0%futurePath%
pip install %~dp0%pefilePath%
pip install %~dp0%altgraphPath%
pip install %~dp0%maconlibPath%
pip install %~dp0%pyInstallerPath%
pause