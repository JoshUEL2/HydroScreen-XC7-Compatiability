<script lang="ts">
    import { invoke } from "@tauri-apps/api/core";
    import { listen } from "@tauri-apps/api/event";
    import { onMount } from "svelte";
    import { fade, fly } from "svelte/transition";

    let showDialog = false;
    let errorMessage = "";

    onMount(async () => {
        await listen<string>("sidecar-error", (event) => {
            console.error("Sidecar Error Received:", event.payload);
            errorMessage = event.payload;
            showDialog = true;
        });
    });

    async function retrySidecar() {
        showDialog = false;
        try {
            console.log("Retrying sidecar spawn...");
            await invoke("retry_sidecar", { debugMode: false });
        } catch (e) {
            console.error("Failed to retry sidecar:", e);
            errorMessage = "Retry failed to send command.";
            showDialog = true;
        }
    }
</script>

{#if showDialog}
    <div
        class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm"
        transition:fade
    >
        <div
            class="glass-modal max-w-md w-full p-6 text-center"
            transition:fly={{ y: 20 }}
        >
            <div class="mb-4 flex justify-center text-red-500">
                <svg
                    xmlns="http://www.w3.org/2000/svg"
                    class="h-12 w-12"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                >
                    <path
                        stroke-linecap="round"
                        stroke-linejoin="round"
                        stroke-width="2"
                        d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                    />
                </svg>
            </div>

            <h2 class="text-xl font-bold text-white mb-2">
                Administrator Access Required
            </h2>
            <p class="text-zinc-400 text-sm mb-6 leading-relaxed">
                The sensor bridge needs administrator privileges to read your
                hardware sensors (CPU, GPU, etc).
            </p>

            {#if errorMessage}
                <div
                    class="bg-red-500/10 border border-red-500/20 rounded-lg p-3 mb-6 text-xs text-red-400 font-mono text-left overflow-x-auto"
                >
                    {errorMessage}
                </div>
            {/if}

            <div class="flex gap-3 justify-center">
                <button
                    on:click={retrySidecar}
                    class="px-6 py-2.5 bg-red-600 hover:bg-red-500 text-white rounded-lg font-medium transition-all shadow-lg hover:shadow-red-500/20 text-sm active:scale-95"
                >
                    Try Again
                </button>
            </div>
        </div>
    </div>
{/if}
