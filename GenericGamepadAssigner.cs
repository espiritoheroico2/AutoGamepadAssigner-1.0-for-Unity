// =============================================================================
//  GenericGamepadAssigner.cs
//  Autor: gerado como utilitário genérico reutilizável
// =============================================================================
//
//  O QUE ESTE SCRIPT FAZ:
//  ----------------------
//  Detecta automaticamente quantos gamepads estão conectados e os distribui
//  entre os players da cena. Cada player precisa ter apenas um componente
//  "PlayerInput" (do pacote Unity Input System) — nenhum script customizado
//  é necessário.
//
//  COMO FUNCIONA O PLAYERINPUT:
//  ----------------------------
//  O componente PlayerInput do Unity já sabe lidar com múltiplos jogadores.
//  Ele possui uma propriedade "devices" que limita quais dispositivos físicos
//  aquele player vai ouvir. Este script apenas preenche essa propriedade
//  com o dispositivo correto para cada player.
//
//  MODOS DE JOGO SUPORTADOS (automático):
//  ----------------------------------------
//  | Dispositivos conectados  | Player 1        | Player 2        |
//  |--------------------------|-----------------|-----------------|
//  | 2+ gamepads              | Gamepad 0       | Gamepad 1       |
//  | 1 gamepad + teclado      | Gamepad         | Teclado         |
//  | Só teclado               | Teclado (livre) | Teclado (livre) |
//  | Nenhum                   | aviso           | aviso           |
//
//  COMO USAR:
//  ----------
//  1. Adicione este script a um GameObject vazio na cena (ex: "GamepadAssigner")
//  2. Cada personagem jogável precisa ter um componente "PlayerInput"
//  3. Preencha os campos "Player 1 Input" e "Player 2 Input" no Inspector,
//     OU preencha as tags e deixe o script encontrar sozinho
//  4. Pronto! Os dispositivos são distribuídos automaticamente no Start()
//     e sempre que um controle for conectado/desconectado.
//
//  REQUISITOS:
//  -----------
//  - Unity Input System (instale via Package Manager)
//  - Cada player precisa ter o componente PlayerInput
//  - O PlayerInput de cada player deve ter um InputActionAsset configurado
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GenericGamepadAssigner : MonoBehaviour
{
    // =========================================================================
    // CONFIGURAÇÃO NO INSPECTOR
    // =========================================================================

    [Header("─── Referências dos Players ───────────────────────────────────")]

    [Tooltip(
        "Componente PlayerInput do primeiro jogador.\n" +
        "Se deixado vazio, o script tenta encontrar automaticamente pela Tag abaixo.")]
    public PlayerInput playerInput1;

    [Tooltip(
        "Componente PlayerInput do segundo jogador.\n" +
        "Se deixado vazio, o script tenta encontrar automaticamente pela Tag abaixo.")]
    public PlayerInput playerInput2;

    [Header("─── Busca Automática por Tag ────────────────────────────────────")]

    [Tooltip(
        "Tag do GameObject do Player 1.\n" +
        "Usada apenas se 'Player Input 1' estiver vazio.")]
    public string tagPlayer1 = "Player";

    [Tooltip(
        "Tag do GameObject do Player 2.\n" +
        "Usada apenas se 'Player Input 2' estiver vazio.")]
    public string tagPlayer2 = "Player2";

    [Header("─── Configurações ───────────────────────────────────────────────")]

    [Tooltip(
        "Se verdadeiro, o script redistribui os dispositivos automaticamente\n" +
        "sempre que um controle for conectado ou desconectado durante o jogo.")]
    public bool autoReassignOnDeviceChange = true;

    [Tooltip(
        "Se verdadeiro, exibe logs detalhados no Console durante a distribuição.\n" +
        "Recomendado deixar ligado durante desenvolvimento.")]
    public bool debugLogs = true;

    // =========================================================================
    // PRIVADOS
    // =========================================================================

    // Guarda os PlayerInputs resolvidos internamente para não depender
    // de os campos públicos estarem preenchidos no Inspector
    private PlayerInput resolvedInput1;
    private PlayerInput resolvedInput2;

    // =========================================================================
    // CICLO DE VIDA UNITY
    // =========================================================================

    private void Awake()
    {
        // Tenta resolver as referências o mais cedo possível.
        // Assim outros scripts que dependam disso já encontram os players prontos.
        ResolveReferences();
    }

    private void Start()
    {
        // Distribui os dispositivos na inicialização.
        // Feito no Start (não no Awake) para garantir que todos os componentes
        // PlayerInput já foram inicializados pelos seus próprios GameObjects.
        AssignDevices();
    }

    private void OnEnable()
    {
        // Registra o listener de mudança de dispositivos.
        // Isso cobre casos como: player conecta um controle durante o jogo,
        // controle cai e desconecta, etc.
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        // IMPORTANTE: sempre remova listeners para evitar memory leaks
        // e chamadas a objetos já destruídos.
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // =========================================================================
    // RESOLUÇÃO DE REFERÊNCIAS
    // =========================================================================

    /// <summary>
    /// Tenta preencher resolvedInput1 e resolvedInput2.
    /// Prioridade: campo público no Inspector > busca por tag na cena.
    /// </summary>
    private void ResolveReferences()
    {
        // --- Player 1 ---
        // Se o campo foi preenchido manualmente no Inspector, usa ele direto
        resolvedInput1 = playerInput1;

        // Se não foi preenchido, tenta encontrar pelo tag configurado
        if (resolvedInput1 == null)
        {
            resolvedInput1 = FindPlayerInputByTag(tagPlayer1);

            if (resolvedInput1 == null)
                Debug.LogWarning($"[GenericGamepadAssigner] Player 1 não encontrado. " +
                                 $"Verifique o campo 'Player Input 1' ou a tag '{tagPlayer1}'.");
        }

        // --- Player 2 ---
        resolvedInput2 = playerInput2;

        if (resolvedInput2 == null)
        {
            resolvedInput2 = FindPlayerInputByTag(tagPlayer2);

            if (resolvedInput2 == null)
                Debug.LogWarning($"[GenericGamepadAssigner] Player 2 não encontrado. " +
                                 $"Verifique o campo 'Player Input 2' ou a tag '{tagPlayer2}'.");
        }
    }

    /// <summary>
    /// Procura na cena um GameObject com a tag informada e retorna
    /// o componente PlayerInput nele (ou null se não encontrar).
    /// </summary>
    /// <param name="tag">A tag a ser buscada na cena.</param>
    private PlayerInput FindPlayerInputByTag(string tag)
    {
        // FindGameObjectWithTag retorna null silenciosamente se a tag não existir
        // no projeto, então protegemos com try/catch para dar uma mensagem clara
        try
        {
            GameObject obj = GameObject.FindGameObjectWithTag(tag);

            if (obj == null)
            {
                Log($"Nenhum objeto com a tag '{tag}' foi encontrado na cena.");
                return null;
            }

            PlayerInput input = obj.GetComponent<PlayerInput>();

            if (input == null)
                Debug.LogWarning($"[GenericGamepadAssigner] O objeto '{obj.name}' (tag: '{tag}') " +
                                 $"não possui um componente PlayerInput.");

            return input;
        }
        catch (UnityException e)
        {
            // Acontece quando a tag nem existe no projeto
            Debug.LogError($"[GenericGamepadAssigner] A tag '{tag}' não existe no projeto Unity. " +
                           $"Crie-a em Edit > Tags and Layers.\nDetalhes: {e.Message}");
            return null;
        }
    }

    // =========================================================================
    // DISTRIBUIÇÃO DE DISPOSITIVOS
    // =========================================================================

    /// <summary>
    /// Método principal. Detecta os dispositivos disponíveis e os distribui
    /// entre os players de acordo com o que está conectado.
    ///
    /// Pode ser chamado manualmente a qualquer momento (ex: menu de configurações,
    /// após uma cena carregar, etc).
    /// </summary>
    public void AssignDevices()
    {
        // Garante que as referências estejam resolvidas antes de tentar usar
        ResolveReferences();

        // Se nem os dois players foram encontrados, não tem o que fazer
        if (resolvedInput1 == null && resolvedInput2 == null)
        {
            Debug.LogError("[GenericGamepadAssigner] Nenhum player encontrado. " +
                           "A distribuição de dispositivos foi cancelada.");
            return;
        }

        // Coleta todos os gamepads físicos conectados no momento
        // Gamepad.all é uma lista somente-leitura mantida automaticamente pelo Input System
        List<Gamepad> gamepads = new List<Gamepad>(Gamepad.all);

        // Referência ao teclado atual (pode ser null se não houver teclado)
        Keyboard keyboard = Keyboard.current;

        Log($"Dispositivos detectados: {gamepads.Count} gamepad(s), " +
            $"teclado: {(keyboard != null ? keyboard.displayName : "nenhum")}");

        // -----------------------------------------------------------------
        // CASO 1: Dois ou mais gamepads conectados
        // Cada player recebe seu próprio controle físico.
        // -----------------------------------------------------------------
        if (gamepads.Count >= 2)
        {
            PairDevicesToPlayer(resolvedInput1, "Player 1", gamepads[0]);
            PairDevicesToPlayer(resolvedInput2, "Player 2", gamepads[1]);

            Log($"Modo: 2 Gamepads → " +
                $"P1: {gamepads[0].displayName} | P2: {gamepads[1].displayName}");
            return;
        }

        // -----------------------------------------------------------------
        // CASO 2: Exatamente um gamepad conectado
        // Player 1 fica com o controle, Player 2 usa o teclado.
        // -----------------------------------------------------------------
        if (gamepads.Count == 1)
        {
            PairDevicesToPlayer(resolvedInput1, "Player 1", gamepads[0]);

            if (keyboard != null)
                PairDevicesToPlayer(resolvedInput2, "Player 2", keyboard);
            else
                Log("Apenas 1 gamepad e nenhum teclado detectado. Player 2 sem dispositivo.");

            Log($"Modo: 1 Gamepad + Teclado → " +
                $"P1: {gamepads[0].displayName} | P2: {(keyboard != null ? keyboard.displayName : "sem dispositivo")}");
            return;
        }

        // -----------------------------------------------------------------
        // CASO 3: Nenhum gamepad — apenas teclado disponível
        // Neste caso, NÃO restringimos o dispositivo de nenhum player.
        // Ambos leem o teclado livremente, cada um com seus próprios
        // bindings (ex: WASD para P1, Setas para P2) definidos no
        // InputActionAsset.
        // -----------------------------------------------------------------
        if (keyboard != null)
        {
            // "Liberar" o player significa não forçar nenhum dispositivo específico,
            // deixando o PlayerInput ouvir qualquer dispositivo compatível com seus bindings
            ReleasePlayerDevices(resolvedInput1, "Player 1");
            ReleasePlayerDevices(resolvedInput2, "Player 2");

            Log("Modo: Só Teclado → Ambos os players leem teclado livremente " +
                "(WASD vs Setas, conforme definido no InputActionAsset).");
            return;
        }

        // -----------------------------------------------------------------
        // CASO 4: Nenhum dispositivo detectado
        // -----------------------------------------------------------------
        Debug.LogWarning("[GenericGamepadAssigner] Nenhum dispositivo de entrada detectado! " +
                         "Conecte um gamepad ou verifique o teclado.");
    }

    // =========================================================================
    // HELPERS DE PAREAMENTO
    // =========================================================================

    /// <summary>
    /// Força um PlayerInput a ouvir exclusivamente o dispositivo informado.
    ///
    /// Como funciona:
    /// O PlayerInput possui uma lista interna de "devices" que filtra quais
    /// dispositivos físicos ele aceita. Ao definir essa lista, garantimos que
    /// dois PlayerInputs com o mesmo InputActionAsset não vão se interferir.
    /// </summary>
    /// <param name="playerInput">O componente PlayerInput do player.</param>
    /// <param name="playerLabel">Nome para exibir no log (ex: "Player 1").</param>
    /// <param name="device">O dispositivo que este player deve usar.</param>
    private void PairDevicesToPlayer(PlayerInput playerInput, string playerLabel, InputDevice device)
    {
        // Proteção: se o player não foi encontrado, ignora silenciosamente
        if (playerInput == null)
        {
            Log($"{playerLabel}: PlayerInput é null, pareamento ignorado.");
            return;
        }

        if (device == null)
        {
            Log($"{playerLabel}: Dispositivo é null, pareamento ignorado.");
            return;
        }

        // Define o dispositivo exclusivo para este PlayerInput.
        // Isso substitui qualquer pareamento anterior.
        // Internamente o Unity chama InputUser.PerformPairingWithDevice por baixo.
        playerInput.SwitchCurrentControlScheme(device);

        Log($"{playerLabel} ({playerInput.gameObject.name}) → {device.displayName}");
    }

    /// <summary>
    /// Remove a restrição de dispositivo de um PlayerInput,
    /// deixando-o ouvir qualquer dispositivo compatível com seus bindings.
    ///
    /// Usado no modo "só teclado", onde ambos os players precisam ler
    /// o mesmo teclado com bindings diferentes.
    /// </summary>
    /// <param name="playerInput">O componente PlayerInput do player.</param>
    /// <param name="playerLabel">Nome para exibir no log.</param>
    private void ReleasePlayerDevices(PlayerInput playerInput, string playerLabel)
    {
        if (playerInput == null) return;

        // Ao passar um array vazio, o PlayerInput volta a aceitar qualquer dispositivo
        // compatível com o control scheme ativo no InputActionAsset
        playerInput.user.UnpairDevices();

        Log($"{playerLabel} ({playerInput.gameObject.name}) → dispositivos liberados (modo teclado livre).");
    }

    // =========================================================================
    // EVENTO: DISPOSITIVO CONECTADO/DESCONECTADO
    // =========================================================================

    /// <summary>
    /// Chamado automaticamente pelo Input System sempre que um dispositivo
    /// é conectado, desconectado, habilitado ou desabilitado.
    /// </summary>
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        // Só nos importa mudanças em gamepads
        // (teclado e mouse raramente mudam em runtime)
        if (device is not Gamepad) return;

        // Só redistribui se a opção estiver habilitada no Inspector
        if (!autoReassignOnDeviceChange) return;

        switch (change)
        {
            case InputDeviceChange.Added:
                Log($"Gamepad conectado: {device.displayName}. Redistribuindo...");
                AssignDevices();
                break;

            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                Log($"Gamepad desconectado: {device.displayName}. Redistribuindo...");
                AssignDevices();
                break;

            // Outros casos (Reconnected, ConfigurationChanged, etc.) são ignorados
            // para evitar redistribuições desnecessárias
        }
    }

    // =========================================================================
    // UTILITÁRIOS
    // =========================================================================

    /// <summary>
    /// Log condicional — só exibe se debugLogs estiver habilitado no Inspector.
    /// Facilita desligar todos os logs de uma vez em produção.
    /// </summary>
    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[GenericGamepadAssigner] {message}");
    }

    // =========================================================================
    // EDITOR — FERRAMENTAS DE DEBUG
    // =========================================================================

#if UNITY_EDITOR
    /// <summary>
    /// Aparece como botão ao clicar com botão direito no componente no Inspector.
    /// Útil para testar a distribuição sem precisar dar Play novamente.
    /// </summary>
    [ContextMenu("Forçar Redistribuição de Dispositivos")]
    private void ForceReassign()
    {
        ResolveReferences();
        AssignDevices();
    }

    /// <summary>
    /// Lista no Console todos os dispositivos detectados pelo Input System.
    /// Útil para diagnosticar problemas de reconhecimento de controles.
    /// </summary>
    [ContextMenu("Listar Dispositivos Conectados")]
    private void ListConnectedDevices()
    {
        var devices = InputSystem.devices;

        if (devices.Count == 0)
        {
            Debug.Log("[GenericGamepadAssigner] Nenhum dispositivo conectado.");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GenericGamepadAssigner] {devices.Count} dispositivo(s) conectado(s):");

        foreach (var d in devices)
            sb.AppendLine($"  • {d.displayName} ({d.GetType().Name}) — ID: {d.deviceId}");

        Debug.Log(sb.ToString());
    }
#endif
}
