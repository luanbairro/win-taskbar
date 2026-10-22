# 🚀 WinTaskbar

> **Monitor moderno de CPU, Memória, Armazenamento e Rede para a barra de tarefas do Windows 11.**

Inspirado nas elegantes extensões de monitoramento do Linux (como o *Astra Monitor* e *Vitals* no GNOME), o **WinTaskbar** traz para o Windows 11 um widget compacto integrado diretamente à barra de tarefas, com flyouts escuros em Fluent Design / Mica, gráficos sparkline com histórico em tempo real e lista dos principais processos consumidores de recursos.

---

## ✨ Recursos

### 🎛️ Barra de Tarefas Compacta (Overlay Integrado)
* **CPU:** Medição em porcentagem em tempo real e ícone dedicado.
* **Memória (RAM):** Mini barra de preenchimento dinâmico indicando a porcentagem de uso.
* **Armazenamento:** Velocidade instantânea de leitura/escrita agregada de disco.
* **Rede:** Taxa de transferência instantânea combinada (Download/Upload).
* **Posicionamento Automático:** Detecta a posição e altura da barra de tarefas do Windows 11 (`Shell_TrayWnd`), ancorando-se próximo à bandeja do sistema. Suporta arrastar e soltar livremente.

---

### 📊 Flyouts Detalhados (Popups ao Clicar)

Cada métrica possui uma janela suspensa individual idêntica aos painéis de monitoramento do sistema:

1. **CPU:**
   * Nome e modelo exato do processador (Intel / AMD).
   * Porcentagem Total, Usuário e Sistema (Kernel).
   * Gráfico de histórico em área (*Sparkline*) dos últimos minutos.
   * Lista dos 5 processos com maior consumo de CPU em tempo real.
   * Tempo de atividade do sistema (*System Uptime*).

2. **Memória (RAM & Swap):**
   * Quantidade Total, Usada e Livre em GB.
   * Barra visual com gradiente e porcentagem.
   * Gráfico contínuo de histórico de consumo de memória.
   * Lista dos 5 processos que mais consomem memória RAM.
   * Monitoramento de Arquivo de Paginação / *Swap* (Usado vs Total).

3. **Armazenamento (Disco):**
   * Gráfico duplo em tempo real (Azul para Leitura, Vermelho para Escrita).
   * Velocidades globais de Leitura e Escrita em MB/s ou KB/s.
   * Lista de todos os discos e partições (`C:`, `D:`, etc.) com tamanho total, porcentagem de ocupação e taxas de transferência individuais.

4. **Rede:**
   * Gráfico duplo com escala automática (Azul para Download, Vermelho para Upload).
   * Taxas globais de Upload e Download em tempo real.
   * Detecção do **IPv4 Público** e do **Gateway Padrão** (Roteador).
   * Lista de todos os adaptadores de rede ativos (Ethernet, Wi-Fi, VPNs como Tailscale) com IPs locais e taxas de transferência dedicadas.

---

## 🛠️ Tecnologias Utilizadas

* **Framework:** [.NET 8.0](https://dotnet.microsoft.com/) (`net8.0-windows`)
* **UI:** Windows Presentation Foundation (**WPF**) com aceleração por hardware (DirectX).
* **Design:** Windows 11 Dark Mode, cantos arredondados nativos (`DwmSetWindowAttribute`) e sombras suaves.
* **APIs de Hardware:**
  * `GlobalMemoryStatusEx` (Win32 Kernel32) para memória ultra-rápida sem overhead.
  * `PerformanceCounter` (PhysicalDisk, Processor).
  * `System.Net.NetworkInformation` para estatísticas de pacotes e tráfego.
  * WMI (`Win32_Processor`) e Registro do Windows para detecção de hardware.
* **Integração com a Shell:** Win32 `FindWindow("Shell_TrayWnd")`, `SHAppBarMessage` e `WS_EX_TOOLWINDOW`.

---

## 📦 Como Compilar e Executar

### Pré-requisitos
* Windows 11 (ou Windows 10) x64
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* Visual Studio 2022 (com carga de trabalho "Desenvolvimento para desktop com .NET") ou VS Code

### 1. Clonar o repositório
```bash
git clone https://github.com/luanbairro/win-taskbar.git
cd win-taskbar
```

### 2. Compilar via linha de comando
```bash
dotnet build -c Release
```

### 3. Gerar Executável Único (Standalone)
Para criar um único arquivo `.exe` pronto para rodar sem dependências adicionais:
```bash
dotnet publish src/WinTaskbar/WinTaskbar.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ./publish
```
O executável final estará disponível na pasta `./publish/WinTaskbar.exe`.

---

## 🤖 Integração Contínua (GitHub Actions)

O repositório já conta com um workflow automatizado em [`.github/workflows/build.yml`](.github/workflows/build.yml). A cada `push` na branch `main`, uma máquina virtual Windows compila o projeto e disponibiliza o executável compilado na aba **Actions** para download imediato.

---

## 📄 Licença

Distribuído sob a licença MIT. Consulte `LICENSE` para obter mais detalhes.
