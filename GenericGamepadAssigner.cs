// =============================================================================
//  GenericGamepadAssigner.cs
//  Utilitário genérico e reutilizável — Unity Input System
// =============================================================================
//
//  O QUE ESTE SCRIPT FAZ:
//  ----------------------
//  Detecta automaticamente os dispositivos conectados (gamepads + teclado)
//  e os distribui exclusivamente entre até 4 jogadores.
//  Cada jogador precisa ter apenas um componente "PlayerInput" no seu
//  GameObject — nenhum script de movimento customizado é necessário.
//
//  LÓGICA DE DISTRIBUIÇÃO (automática, por prioridade):
//  -----------------------------------------------------
//  Os players são processados na ordem da lista (slot 0, 1, 2, 3...).
//  Os gamepads são distribuídos primeiro, na ordem em que foram conectados.
//  Se sobrarem players sem gamepad, o teclado é atribuído ao próximo da fila.
//  Players além dos dispositivos disponíveis ficam sem dispositivo.
//
//  Exemplos:
//  ┌─────────────────────────────┬────────┬────────┬────────┬────────┐
//  │ Dispositivos conectados     │  P1    │  P2    │  P3    │  P4    │
//  ├─────────────────────────────┼────────┼────────┼────────┼────────┤
//  │ 4 gamepads                  │ GP[0]  │ GP[1]  │ GP[2]  │ GP[3]  │
//  │ 3 gamepads                  │ GP[0]  │ GP[1]  │ GP[2]  │ teclado│
//  │ 2 gamepads                  │ GP[0]  │ GP[1]  │ teclado│  —     │
//  │ 1 gamepad                   │ GP[0]  │ teclado│  —     │  —     │
//  │ Só teclado                  │ livre  │ livre  │ livre  │ livre  │
//  └─────────────────────────────┴────────┴────────┴────────┴────────┘
//  (livre = sem InputUser restrito; todos leem o teclado pelos próprios bindings)
//
//  COMO USAR:
//  ----------
//  1. Adicione este script a um GameObject vazio na cena (ex: "GamepadAssigner")
//  2. Cada personagem jogável precisa ter um componente "PlayerInput"
//  3. No Inspector, configure a lista "Players" com até 4 entradas:
//       - Arraste o componente PlayerInput diretamente, OU
//       - Deixe o campo vazio e preencha a Tag para busca automática
//  4. Pronto! A distribuição acontece no Start() e se atualiza automaticamente
//     quando controles são conectados ou desconectados.
//
//  REQUISITOS:
//  -----------
//  - Unity Input System instalado (Package Manager)
//  - Cada player precisa ter o componente PlayerInput com um InputActionAsset
//
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GenericGamepadAssigner : MonoBehaviour
{
    // =========================================================================
    // ESTRUTURA DE DADOS: SLOT DE JOGADOR
    // =========================================================================

    /// <summary>
    /// Representa a configuração de um jogador no Inspector.
    /// Cada slot pode ser preenchido com uma referência direta ao PlayerInput
    /// ou com uma tag para busca automática na cena — ou ambos.
    /// </summary>
    [System.Serializable]
    public class PlayerSlot
    {
        [Tooltip(
            "Nome exibido nos logs para identificar este jogador.\n" +
            "Ex: 'Player 1', 'Player 3', etc.")]
        public string label = "Player";

        [Tooltip(
            "Referência direta ao componente PlayerInput deste jogador.\n" +
            "Se preenchido, a Tag abaixo é ignorada.")]
        public PlayerInput playerInput;

        [Tooltip(
            "Tag do GameObject deste jogador na cena.\n" +
            "Usada apenas se o campo 'Player Input' acima estiver vazio.")]
        public string tag = "";

        // Referência resolvida internamente (não aparece no Inspector)
        // Pode ser diferente de playerInput se foi encontrado via tag
        [System.NonSerialized]
        public PlayerInput resolvedInput;
    }

    // =========================================================================
    // CONFIGURAÇÃO NO INSPECTOR
    // =========================================================================

    [Header("─── Jogadores (até 4) ──────────────────────────────────────────")]
    [Tooltip(
        "Lista de jogadores. Adicione de 1 a 4 entradas.\n" +
        "A ordem importa: o primeiro da lista recebe o primeiro gamepad,\n" +
        "o segundo recebe o segundo, e assim por diante.")]
    public List<PlayerSlot> players = new List<PlayerSlot>()
    {
        // Valores padrão pré-configurados para conveniência
        new PlayerSlot { label = "Player 1", tag = "Player"  },
        new PlayerSlot { label = "Player 2", tag = "Player2" },
        new PlayerSlot { label = "Player 3", tag = "Player3" },
        new PlayerSlot { label = "Player 4", tag = "Player4" },
    };

    [Header("─── Configurações ───────────────────────────────────────────────")]

    [Tooltip(
        "Se verdadeiro, redistribui os dispositivos automaticamente\n" +
        "sempre que um controle for conectado ou desconectado durante o jogo.")]
    public bool autoReassignOnDeviceChange = true;

    [Tooltip(
        "Se verdadeiro, exibe logs detalhados no Console.\n" +
        "Recomendado manter ligado durante desenvolvimento.")]
    public bool debugLogs = true;

    // =========================================================================
    // CICLO DE VIDA UNITY
    // =========================================================================

    private void Awake()
    {
        // Resolve as referências o mais cedo possível para que outros scripts
        // já encontrem tudo pronto durante seus próprios Awakes/Starts
        ResolveAllReferences();
    }

    private void Start()
    {
        // Distribui os dispositivos no Start (não no Awake) para garantir
        // que todos os componentes PlayerInput já foram inicializados
        AssignDevices();
    }

    private void OnEnable()
    {
        // Escuta mudanças de dispositivos em tempo real
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        // IMPORTANTE: sempre remova listeners para evitar memory leaks
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    // =========================================================================
    // RESOLUÇÃO DE REFERÊNCIAS
    // =========================================================================

    /// <summary>
    /// Percorre todos os slots e resolve o PlayerInput de cada um.
    /// Prioridade: campo direto no Inspector > busca por tag.
    /// </summary>
    private void ResolveAllReferences()
    {
        // Limita a 4 slots para segurança
        // (mais que isso geralmente não faz sentido para local multiplayer)
        if (players.Count > 4)
        {
            Debug.LogWarning($"[GenericGamepadAssigner] {players.Count} slots configurados. " +
                              "O máximo recomendado é 4. Os slots excedentes serão ignorados.");
        }

        int limit = Mathf.Min(players.Count, 4);

        for (int i = 0; i < limit; i++)
        {
            PlayerSlot slot = players[i];

            // --- Prioridade 1: referência direta no Inspector ---
            if (slot.playerInput != null)
            {
                slot.resolvedInput = slot.playerInput;
                Log($"Slot {i} ({slot.label}): usando referência direta → {slot.playerInput.gameObject.name}");
                continue;
            }

            // --- Prioridade 2: busca por tag ---
            if (!string.IsNullOrEmpty(slot.tag))
            {
                slot.resolvedInput = FindPlayerInputByTag(slot.tag, slot.label);
                continue;
            }

            // --- Nenhuma das duas configuradas ---
            Debug.LogWarning($"[GenericGamepadAssigner] Slot {i} ({slot.label}): " +
                              "nem 'Player Input' nem 'Tag' foram configurados. Este slot será ignorado.");
            slot.resolvedInput = null;
        }
    }

    /// <summary>
    /// Busca um GameObject pela tag informada e retorna seu PlayerInput.
    /// Retorna null com log de aviso se não encontrar.
    /// </summary>
    /// <param name="tag">Tag a ser buscada na cena.</param>
    /// <param name="label">Nome do slot para exibir no log.</param>
    private PlayerInput FindPlayerInputByTag(string tag, string label)
    {
        try
        {
            GameObject obj = GameObject.FindGameObjectWithTag(tag);

            if (obj == null)
            {
                Debug.LogWarning($"[GenericGamepadAssigner] ({label}): " +
                                 $"Nenhum objeto com a tag '{tag}' foi encontrado na cena.");
                return null;
            }

            PlayerInput input = obj.GetComponent<PlayerInput>();

            if (input == null)
                Debug.LogWarning($"[GenericGamepadAssigner] ({label}): " +
                                 $"O objeto '{obj.name}' (tag: '{tag}') não tem componente PlayerInput.");
            else
                Log($"({label}): encontrado por tag '{tag}' → {obj.name}");

            return input;
        }
        catch (UnityException e)
        {
            // Disparado quando a tag não existe no projeto (nem está cadastrada)
            Debug.LogError($"[GenericGamepadAssigner] ({label}): " +
                           $"A tag '{tag}' não existe no projeto. " +
                           $"Crie-a em Edit > Tags and Layers.\nDetalhes: {e.Message}");
            return null;
        }
    }

    // =========================================================================
    // DISTRIBUIÇÃO DE DISPOSITIVOS — MÉTODO PRINCIPAL
    // =========================================================================

    /// <summary>
    /// Detecta os dispositivos disponíveis e os distribui entre os slots
    /// configurados, na ordem da lista.
    ///
    /// Pode ser chamado manualmente a qualquer momento — por exemplo,
    /// ao carregar uma nova cena ou abrir um menu de configuração.
    /// </summary>
    public void AssignDevices()
    {
        // Garante que as referências estejam atualizadas antes de distribuir
        ResolveAllReferences();

        // Monta a fila de dispositivos disponíveis na ordem de prioridade:
        // gamepads primeiro (na ordem de conexão), teclado por último
        List<InputDevice> availableDevices = BuildDeviceQueue();

        Log($"Fila de dispositivos montada: [{string.Join(", ", availableDevices.ConvertAll(d => d.displayName))}]");

        // Conta quantos players foram resolvidos com sucesso
        int resolvedCount = 0;

        int limit = Mathf.Min(players.Count, 4);

        for (int i = 0; i < limit; i++)
        {
            PlayerSlot slot = players[i];

            // Slot sem PlayerInput válido: pula
            if (slot.resolvedInput == null)
            {
                Log($"Slot {i} ({slot.label}): sem PlayerInput, pulando.");
                continue;
            }

            resolvedCount++;

            // Verifica se há um dispositivo disponível para este slot
            if (i < availableDevices.Count)
            {
                // Há um dispositivo na fila para este player
                InputDevice device = availableDevices[i];

                if (device is Keyboard)
                {
                    // Teclado é compartilhado — não restringe via InputUser
                    // Cada PlayerInput lerá o teclado pelos seus próprios bindings
                    ReleasePlayerDevices(slot.resolvedInput, slot.label);
                    Log($"Slot {i} ({slot.label}): teclado livre " +
                        "(lê pelo binding do InputActionAsset).");
                }
                else
                {
                    // Gamepad: pareia exclusivamente com este player
                    PairDeviceToPlayer(slot.resolvedInput, slot.label, device);
                }
            }
            else
            {
                // Sem dispositivo disponível para este slot
                ReleasePlayerDevices(slot.resolvedInput, slot.label);
                Log($"Slot {i} ({slot.label}): sem dispositivo disponível.");
            }
        }

        if (resolvedCount == 0)
            Debug.LogError("[GenericGamepadAssigner] Nenhum PlayerInput foi encontrado. " +
                           "Verifique as referências e tags configuradas.");
    }

    /// <summary>
    /// Monta uma lista ordenada de dispositivos disponíveis para distribuição.
    ///
    /// Ordem:
    ///   1. Gamepads conectados (na ordem em que foram plugados — Gamepad.all)
    ///   2. Teclado (se disponível), como última opção
    ///
    /// O teclado entra na fila mas é tratado de forma especial em AssignDevices():
    /// ao invés de restringir, liberamos o PlayerInput para ler livremente,
    /// pois múltiplos players podem precisar ler o mesmo teclado com bindings
    /// diferentes (ex: WASD vs Setas).
    /// </summary>
    private List<InputDevice> BuildDeviceQueue()
    {
        List<InputDevice> queue = new List<InputDevice>();

        // Adiciona todos os gamepads na ordem de conexão
        foreach (Gamepad gp in Gamepad.all)
            queue.Add(gp);

        // Adiciona o teclado ao final (se existir)
        if (Keyboard.current != null)
            queue.Add(Keyboard.current);

        return queue;
    }

    // =========================================================================
    // HELPERS DE PAREAMENTO
    // =========================================================================

    /// <summary>
    /// Restringe um PlayerInput a ouvir exclusivamente um dispositivo.
    ///
    /// Internamente, SwitchCurrentControlScheme() chama InputUser para
    /// fazer o pareamento. Após isso, o PlayerInput ignora qualquer outro
    /// dispositivo físico — impedindo interferências entre players.
    /// </summary>
    /// <param name="playerInput">Componente PlayerInput do jogador.</param>
    /// <param name="label">Nome para exibir no log.</param>
    /// <param name="device">Dispositivo a ser pareado exclusivamente.</param>
    private void PairDeviceToPlayer(PlayerInput playerInput, string label, InputDevice device)
    {
        if (playerInput == null || device == null) return;

        playerInput.SwitchCurrentControlScheme(device);

        Log($"({label}) [{playerInput.gameObject.name}] → pareado com: {device.displayName}");
    }

    /// <summary>
    /// Remove a restrição de dispositivo de um PlayerInput.
    ///
    /// Após chamar isso, o PlayerInput voltará a aceitar qualquer dispositivo
    /// compatível com os bindings do seu InputActionAsset. Útil para o modo
    /// teclado, onde múltiplos players compartilham o mesmo dispositivo físico.
    /// </summary>
    /// <param name="playerInput">Componente PlayerInput do jogador.</param>
    /// <param name="label">Nome para exibir no log.</param>
    private void ReleasePlayerDevices(PlayerInput playerInput, string label)
    {
        if (playerInput == null) return;

        // UnpairDevices() desvincula todos os dispositivos associados ao InputUser
        // deste PlayerInput, liberando-o para leitura genérica
        playerInput.user.UnpairDevices();

        Log($"({label}) [{playerInput.gameObject.name}] → dispositivos liberados.");
    }

    // =========================================================================
    // EVENTO: DISPOSITIVO CONECTADO / DESCONECTADO
    // =========================================================================

    /// <summary>
    /// Listener chamado pelo Input System sempre que qualquer dispositivo
    /// muda de estado (conectado, desconectado, reconfigurado, etc.).
    /// </summary>
    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        // Só reagimos a gamepads — teclado e mouse raramente mudam em runtime
        if (device is not Gamepad) return;

        // Respeita o toggle do Inspector
        if (!autoReassignOnDeviceChange) return;

        switch (change)
        {
            case InputDeviceChange.Added:
                Log($"Gamepad conectado: '{device.displayName}'. Redistribuindo...");
                AssignDevices();
                break;

            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                Log($"Gamepad desconectado: '{device.displayName}'. Redistribuindo...");
                AssignDevices();
                break;

            // Reconnected, ConfigurationChanged, etc. são ignorados intencionalmente
            // para evitar redistribuições desnecessárias durante o jogo
        }
    }

    // =========================================================================
    // UTILITÁRIOS
    // =========================================================================

    /// <summary>
    /// Log interno condicional. Respeita o toggle "debugLogs" do Inspector.
    /// Use isso no lugar de Debug.Log() direto para facilitar silenciar em produção.
    /// </summary>
    private void Log(string message)
    {
        if (debugLogs)
            Debug.Log($"[GenericGamepadAssigner] {message}");
    }

    // =========================================================================
    // EDITOR — FERRAMENTAS DE DEBUG (só visíveis no Unity Editor)
    // =========================================================================

#if UNITY_EDITOR

    /// <summary>
    /// Botão no Inspector (botão direito no componente).
    /// Força a redistribuição imediata sem precisar reiniciar o Play.
    /// </summary>
    [ContextMenu("Forçar Redistribuição de Dispositivos")]
    private void ForceReassign()
    {
        ResolveAllReferences();
        AssignDevices();
    }

    /// <summary>
    /// Botão no Inspector.
    /// Lista no Console todos os dispositivos que o Input System detectou.
    /// Útil para diagnosticar controles que não aparecem ou têm nome errado.
    /// </summary>
    [ContextMenu("Listar Dispositivos Conectados")]
    private void ListConnectedDevices()
    {
        var devices = InputSystem.devices;

        if (devices.Count == 0)
        {
            Debug.Log("[GenericGamepadAssigner] Nenhum dispositivo conectado no momento.");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GenericGamepadAssigner] {devices.Count} dispositivo(s) detectado(s):");

        foreach (var d in devices)
            sb.AppendLine($"  • {d.displayName}  |  Tipo: {d.GetType().Name}  |  ID: {d.deviceId}");

        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Botão no Inspector.
    /// Exibe o estado atual de pareamento de cada slot configurado.
    /// </summary>
    [ContextMenu("Exibir Estado dos Slots")]
    private void PrintSlotStatus()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[GenericGamepadAssigner] Estado dos slots:");

        int limit = Mathf.Min(players.Count, 4);

        for (int i = 0; i < limit; i++)
        {
            PlayerSlot slot = players[i];
            string inputName   = slot.resolvedInput != null ? slot.resolvedInput.gameObject.name : "não resolvido";
            string deviceNames = "nenhum";

            if (slot.resolvedInput != null && slot.resolvedInput.user.valid)
            {
                var pairedDevices = slot.resolvedInput.user.pairedDevices;
                if (pairedDevices.Count > 0)
                    deviceNames = string.Join(", ", System.Linq.Enumerable.Select(pairedDevices, d => d.displayName));
            }

            sb.AppendLine($"  Slot {i} | {slot.label} | GameObject: {inputName} | Dispositivos: {deviceNames}");
        }

        Debug.Log(sb.ToString());
    }

#endif
}