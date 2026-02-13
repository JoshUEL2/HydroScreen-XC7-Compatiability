<script lang="ts">
    import { invoke } from "@tauri-apps/api/core";
    import { listen } from "@tauri-apps/api/event";
    import { onMount } from "svelte";
    import { fade, scale } from "svelte/transition";
    import { ShieldAlert } from "lucide-svelte";

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
        class="fixed inset-0 z-[150] bg-black/60 backdrop-blur-sm flex items-center justify-center p-8 outline-none"
        transition:fade={{ duration: 200 }}
    >
        <div
            class="glass-modal w-full max-w-sm p-6 text-center"
            transition:scale={{ duration: 200, start: 0.95 }}
            role="alertdialog"
            aria-modal="true"
        >
            <div
                class="w-12 h-12 bg-red-500/10 rounded-full flex items-center justify-center mx-auto mb-4 text-red-500"
            >
                <ShieldAlert size={24} />
            </div>

            <h2 class="text-lg font-bold text-white mb-2">
                Admin Access Required
            </h2>
            <p
                class="text-zinc-400 text-sm mb-6 leading-relaxed font-mono bg-black/20 p-3 rounded-lg border border-white/5 break-words text-left"
            >
                The sensor bridge needs administrator privileges to read
                hardware sensors.
                {#if errorMessage}
                    <br /><br />
                    <span class="text-red-400">Error: {errorMessage}</span>
                {/if}
            </p>

            <button
                on:click={retrySidecar}
                class="w-full px-6 py-2 rounded-lg bg-red-600 hover:bg-red-500 text-white font-medium transition-colors shadow-lg shadow-red-500/20"
            >
                Try Again
            </button>
        </div>
    </div>
{/if}
