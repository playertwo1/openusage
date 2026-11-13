# Auditoria das releases do OpenUsage original

Corte original: 8 de novembro de 2026. Complemento atualizado em 13 de novembro de 2026.

## Recorte

Considerei o início deste fork como 28 de outubro de 2026, data de criação do repositório fork, e confirmei que o primeiro commit Windows é de 29 de outubro. Assim, as releases publicadas depois do fork são as cinco betas 0.8.0 abaixo. A 0.7.16, de 26 de outubro, é a referência imediatamente anterior. A linha Swift/macOS da origem tem histórico desde junho, mas isso antecede a criação deste fork.

Fontes de contexto: [metadata do fork](https://api.github.com/repos/playertwo1/openusage), [primeiro commit Windows](https://github.com/playertwo1/openusage/commit/b8aa05a4d7d47fa0106e2d8a016c6058a0cfaeee), [release 0.7.16](https://github.com/robinebers/openusage/releases/tag/v0.7.16) e [README do fork](https://github.com/playertwo1/openusage#openusage-for-windows). O README informa que Windows e macOS têm builds e releases separados.

## Releases posteriores ao fork

| Release | Novidades principais | Avaliação para o fork Windows |
|---|---|---|
| [0.8.0-beta.1 · 4 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.1) | Refez a edição macOS em torno de login dentro do app e dados reportados pelas APIs; removeu leitura de credenciais e logs de outras ferramentas, estimativas locais de custo/tokens e histórico iCloud. Também introduziu a tela Priority, separada de All Usage. | A direção de privacidade e dados já combina com o Windows daqui: contas conectadas no app e métricas obtidas das APIs dos provedores. A migração Swift, a integração com Keychain/Sparkle e alterações de menu bar são específicas do Mac. A ideia de destacar limites urgentes pode ser reaproveitada, mas o Windows já tem gráficos de ritmo semanal e alertas básicos. |
| [0.8.0-beta.2 · 4 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.2) | Z.ai passou a mostrar e consumir resets acumulados; entrou o provedor Muse Code com login por código; vieram limites de prioridade configuráveis, limites “burning fast” e alerta Use It or Lose It. Corrigiu moeda do Extra Usage do Claude e rótulos de gasto do Cursor Team e Claude Enterprise. | **Maior oportunidade direta:** o mapper Z.ai do Windows lê percentuais e nextResetTime, mas não resets acumulados. Prioridade/alertas podem aproveitar o WeeklyPace e o sistema de notificações atuais. Muse é um provedor novo e só compensa se houver demanda. Moeda do Claude é melhoria de correção; os rótulos de Claude/Cursor são uma melhoria de clareza, com risco para preferências salvas por nome de métrica. |
| [0.8.0-beta.3 · 5 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.3) | Lista limites que ainda podem ser usados antes do reset; login Copilot em domínios GitHub Enterprise no GHE.com; marcador de ritmo uniforme em barras e anéis; botão para copiar ao lado da marca do provedor; ajuda contextual e “Used Up” por último. | O Windows já mostra o ritmo uniforme nas barras e o gráfico semanal inclui a linha de ritmo médio; essa parte está coberta. Ainda não há uma lista que priorize limites que vencem antes. GHE Copilot é útil apenas para contas Enterprise: o login e o uso atuais apontam diretamente para github.com/api.github.com. O restante é acabamento de UI; tooltips não devem ser portados sem pedido explícito. |
| [0.8.0-beta.4 · 6 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.4) | Oculta nomes de conta durante compartilhamento de tela; opção para espelhar a ordem Priority na menu bar; ajustes de animação e tempo de reset; correção do painel translúcido no macOS 26. | A preferência manual Hide Account Names já existe no Windows e afeta tray/notificações. Detecção automática de captura e menu bar usam APIs/comportamentos do macOS, então não são ports diretos. Animação e correção de frost também não se aplicam à UI Windows atual. |
| [0.8.0-beta.5 · 8 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.5) | Dados aleatórios reproduzíveis para builds de desenvolvimento; abre configurações de notificação com um clique; ajusta nomes de plano Claude e encaixe de relógio/percentual; atualiza SDK de telemetria e testes. | O ajuste de configurações e o layout citados são correções da UI Mac. Os dados de desenvolvimento são úteis somente se nossos dados de demonstração precisarem ser aleatórios e reproduzíveis; os fixtures Windows existentes já são determinísticos. SDK, refactors e testes Swift não devem ser portados. |

## O que já existe neste fork

- O Windows faz login a partir do app e consulta APIs dos provedores; não precisa da mudança arquitetural da beta.1.
- Antigravity Argon já é mapeado e coberto pelos checks locais.
- WeeklyPace já calcula ritmo/projeção semanal e aparece no painel e no widget.
- Há Low Usage Alerts. Hoje eles são um único limite fixo de 90% usado (menos de 10% restante), não os níveis configuráveis e alertas de reset/ritmo das betas novas.
- A 0.7.16 já trazia Argon e correções de uso Enterprise/Team do Cursor; o Windows tem suporte a Argon e leitura de uso de equipe. Portanto, essas partes não são novidade pendente.

Referências do código Windows: Windows/OpenUsage.Windows/UsageMappers.cs, UsageMappers.Antigravity.cs, UsageMappers.Cursor.cs, UsageService.cs, ProviderLogin.Device.cs, WeeklyPace.cs, MainForm.cs, ShellView.Settings.cs e AccountStore.cs.

## Recomendações

1. **Implementar Z.ai banked resets primeiro**, se o payload atual da API os fornecer. É a lacuna de provider mais concreta e localizada. Preservar os valores reportados pelo provedor e acrescentar um fixture de regressão.
2. **Depois, melhorar prioridade e alertas reutilizando o que já existe.** O melhor recorte inicial é configurar níveis de aviso e distinguir limite que está acabando rápido de limite que vai expirar com saldo sobrando. Não é necessário copiar toda a tela Priority do Mac para obter o benefício.
3. **Corrigir moeda/semântica do Extra Usage do Claude e tornar explícito o gasto Enterprise/Team**, após confirmar quais campos a resposta atual do provedor oferece. O mapper Windows assume USD para Claude e usa “Extra Usage” de forma genérica. Há um cuidado de compatibilidade: Pins, métricas ocultas e ordenação são indexados pelo rótulo da métrica em AccountStore; simplesmente renomear “Extra Usage” pode deixar essas preferências antigas órfãs.
4. **Adicionar GHE.com ao Copilot somente se houver usuários Enterprise que precisem disso.** O host de login, a consulta de usuário e a API de uso precisam seguir o domínio correto; não basta trocar o link de autenticação.
5. **Adicionar Muse somente com demanda confirmada.** É um provider inteiro novo, com autenticação por código, consulta e mapeamento próprios. Antes de implementar métricas novas, as regras do projeto exigem confirmar com o owner os defaults de visibilidade e ordem.

## Não portaria diretamente

Não cherry-pickar código Swift para o app C# Windows. Reaproveitar as ideias ou regras de mapeamento quando fizerem sentido. As alterações de SwiftUI/AppKit, Sparkle, Keychain, PostHog iOS, menu bar, transições/macOS 26 e os testes da edição original pertencem à outra implementação. A funcionalidade automática de privacidade durante compartilhamento também exige um desenho e validação específicos para Windows.

Esta auditoria é leitura de releases e código local; não alterou o app nem executou testes.

## Complemento — releases até 13 de novembro de 2026

O corte anterior parava na beta.5. Desde a criação da fork em 28 de outubro, o original publicou
oito betas 0.8.0 e a estável 0.8.0. A estável aponta para o mesmo commit da beta.8 (`35e88e9`), sem
mudança de código adicional entre elas. Considerei o branch `windows-personal-usage-phase` em
`c7b12ca`, que contém o trabalho Windows mais recente desta fork. As recomendações a seguir
substituem as anteriores quando o estado do branch ou a decisão do owner mudou.

| Release | Novidades após o corte anterior | Decisão para Windows |
|---|---|---|
| [0.8.0-beta.6 · 9 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.6) | Esconder métrica pelo menu de contexto; excluir limites usados do contador `+N` da menu bar. | Esconder métrica já existe no menu de contexto do Windows. O contador `+N` da menu bar é específico do Mac. |
| [0.8.0-beta.7 · 10 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.7) | Nomear resets e escolher qual gastar; corrigir quotas Gemini ativas/resets do Antigravity; correções de CLIProxyAPI e transições de abas. | Planejar a ação de Claude/Codex para o Windows com confirmação e revalidação do reset escolhido (ver roadmap). O commit `c7b12ca` já consulta o host diário do Antigravity quando o host principal indica uma janela ainda não iniciada; a beta.7 passou a preferir o host diário e usar produção como fallback. Comparar com dados reais antes de ampliar a mudança. CLIProxyAPI e animações de abas não fazem parte deste app. |
| [0.8.0-beta.8 · 11 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0-beta.8) | Adicionar Amp e login OpenCode Console; unificar contas; separar prioridade e notificação dos alertas; corrigir alertas em resets móveis, leitura de grants Claude, gasto do Cursor, cap Grok e datas do OpenRouter. | As prioridades e alertas equivalentes já estão no branch Windows. Os demais itens de UI não justificam cópia direta. Os bugs do Cursor, Grok e reset móvel têm correspondência concreta no código Windows e são os ports mais claros. |
| [0.8.0 · 12 nov.](https://github.com/robinebers/openusage/releases/tag/v0.8.0) | Release estável da beta.8, no mesmo commit. | Nenhum delta de código adicional a portar. |

### Estado do branch Windows

- O commit `f39da88` já implementa Extra Usage do Claude na moeda/precisão da conta, limiares
  Warning/Critical, filtro Priority, Burning Fast e alertas Use It or Lose It. Não duplicar esse
  trabalho.
- O mesmo commit remove Z.ai da edição Windows. Por isso, os resets acumulados do Z.ai, que eram a
  principal sugestão da auditoria de 8 de novembro, deixam de ser candidatos para este produto.
- O Windows já pode ocultar métricas pelo menu de contexto. A privacidade por nome também é manual;
  detecção automática de captura de tela é específica do Mac.
- O branch ainda mostra o plano Go do OpenCode com API key. O novo login de console do Mac só vale
  uma mudança se houver demanda explícita por OAuth no Windows.

### Ports recomendados

1. **Grok — corrigir unidade do cap.** `UsageMappers.Grok` trata `onDemandCap.val` como contagem e
   mostra, por exemplo, `2500 Cap`. A origem confirma que xAI envia centavos; converter para dólares
   antes de mostrar. [Correção upstream](https://github.com/robinebers/openusage/commit/b7d81db2b99d13c145ad0f4c5e4583373d53510b).
2. **Cursor — manter limite e gasto do mesmo orçamento.** O mapper escolhe primeiro
   `individualLimit`/`pooledLimit`, mas aceita o primeiro campo positivo entre `individualUsed`,
   `pooledUsed` e `totalSpend`; isso pode misturar o cap pessoal com o gasto da equipe. Usar o par
   correspondente, como na [correção upstream](https://github.com/robinebers/openusage/commit/b3f6e7920d54318b4fb02ccd08d878abaea139e5).
3. **Alertas — não rearmar quando o reset apenas avança.** `UsageAlertEvaluator` considera qualquer
   novo `ResetsAt` um ciclo novo. Para sessões móveis como OpenCode, isso pode repetir Warning,
   Critical ou Burning Fast antes do reset anterior. Só iniciar novo ciclo quando o reset anterior
   passar, como na [correção upstream](https://github.com/robinebers/openusage/commit/f84068b35d39720b4ae2047ad6676b7707eaad3e).
4. **Antigravity — revisar a prioridade dos hosts.** O Windows tenta produção primeiro e consulta o
   host diário quando a resposta está incompleta ou parece não iniciada; a correção do Mac agora
   prefere diário e recorre à produção. O código Windows já cobre parte do caso; só mudar após validar
   fixtures de quotas iniciadas, não iniciadas e ausentes. [Correção upstream](https://github.com/robinebers/openusage/commit/ee9815937de6d52ae490f2d8de2412fadc26916d).
5. **Claude — revisar cache de leitura secundária.** `Hourly` guarda o resultado `null` quando `Optional`
   registra falha, então uma falha transitória ao ler grants pode ocultar os resets por até uma hora.
   A beta.8 passou a tentar de novo uma primeira leitura de grants que falhou. Aplicar uma política
   de retry com o mesmo cuidado de rate limit do Claude.

### Outras oportunidades que valem considerar

- **Gastar um reset escolhido (Claude/Codex):** valor alto para quem recebe resets extras; hoje o
  Windows mostra a contagem, mas não deixa usar um. Incluído no roadmap como ação manual com
  confirmação explícita, revalidação e tratamento de resultado incerto.
- **Histórico de uso fornecido pelas APIs:** a 0.8.0 adiciona períodos diários e detalhes de tokens,
  eventos e atividade para ChatGPT, Cursor, Ollama e OpenRouter. Pode dar uma visão mais completa,
  mas amplia o produto além dos limites e valores monetários atuais. A regra do projeto para o
  dashboard hoje restringe os dados a limites e valores em dólares; está no roadmap como uma tela
  separada, usando apenas dados reportados pelos provedores. Não adicionará métricas ao dashboard.
- **Unificar contas do mesmo provedor:** pode reduzir cartões repetidos para quem usa várias contas;
  priorizar apenas se a lista atual estiver ficando difícil de ler.
- **Privacidade automática durante compartilhamento de tela:** complemento à ocultação manual de
  nomes que já existe. O Mac detecta captura; o Windows precisaria de um desenho próprio para detectar
  compartilhamento/captura com comportamento previsível.
- **Login de OpenCode Console:** conveniência em relação à API key atual; o uso Go já é lido. Só
  compensa se o fluxo por chave estiver causando atrito.
- **Amp e Muse Code:** provedores novos. Implementar quando houver usuários que precisem deles; pedir
  ao owner os defaults de métricas antes de adicionar métricas novas.
- **Copilot em `GHE.com`:** útil para contas GitHub Enterprise nessa hospedagem; fora do foco pessoal
  atual.
- **Não portar diretamente:** menu bar, SwiftUI/AppKit, Sparkle, CLI/macOS, transições e ajustes de
  painel. Essas partes são específicas da edição Mac.

Esta atualização também foi somente leitura do código do app; atualizei apenas este relatório e não
executei testes.
