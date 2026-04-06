package com.max.reel.ui.library

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewModelScope
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.tv.material3.ExperimentalTvMaterial3Api
import androidx.tv.material3.Surface
import androidx.tv.material3.Text
import com.max.reel.data.ApiClient
import com.max.reel.data.LibraryResult
import com.max.reel.model.FileNode
import com.max.reel.model.NodeType
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class DirectoryLevel(
    val title: String,
    val items: List<FileNode>,
)

sealed interface LibraryUiState {
    data object Loading : LibraryUiState
    data class Error(val message: String) : LibraryUiState
    data class Success(val stack: List<DirectoryLevel>) : LibraryUiState
}

class LibraryViewModel(private val apiClient: ApiClient) : ViewModel() {

    private val _state = MutableStateFlow<LibraryUiState>(LibraryUiState.Loading)
    val state: StateFlow<LibraryUiState> = _state.asStateFlow()

    init {
        loadLibrary()
    }

    fun loadLibrary() {
        _state.value = LibraryUiState.Loading
        viewModelScope.launch {
            when (val result = apiClient.fetchLibrary()) {
                is LibraryResult.Success -> {
                    val root = result.root
                    val children = sortNodes(root.children ?: emptyList())
                    _state.value = LibraryUiState.Success(
                        stack = listOf(DirectoryLevel(root.name, children))
                    )
                }
                is LibraryResult.Unauthorized -> {
                    _state.value = LibraryUiState.Error("Invalid key — please re-pair")
                }
                is LibraryResult.NetworkError -> {
                    _state.value = LibraryUiState.Error(result.message)
                }
            }
        }
    }

    fun enterDirectory(node: FileNode) {
        val current = _state.value
        if (current !is LibraryUiState.Success) return
        val children = sortNodes(node.children ?: emptyList())
        _state.update {
            LibraryUiState.Success(
                stack = current.stack + DirectoryLevel(node.name, children)
            )
        }
    }

    /** Returns false if already at root (caller should let system handle back). */
    fun navigateUp(): Boolean {
        val current = _state.value
        if (current !is LibraryUiState.Success) return false
        if (current.stack.size <= 1) return false
        _state.update {
            LibraryUiState.Success(stack = current.stack.dropLast(1))
        }
        return true
    }

    private fun sortNodes(nodes: List<FileNode>): List<FileNode> =
        nodes.sortedWith(compareBy<FileNode> { it.type != NodeType.DIRECTORY }.thenBy { it.name.lowercase() })
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
fun LibraryScreen(apiClient: ApiClient, onPlayFile: (String) -> Unit) {
    val viewModel: LibraryViewModel = viewModel(
        factory = object : ViewModelProvider.Factory {
            @Suppress("UNCHECKED_CAST")
            override fun <T : ViewModel> create(modelClass: Class<T>): T {
                return LibraryViewModel(apiClient) as T
            }
        }
    )

    val state by viewModel.state.collectAsStateWithLifecycle()

    when (val current = state) {
        is LibraryUiState.Loading -> LoadingContent()
        is LibraryUiState.Error -> ErrorContent(
            message = current.message,
            onRetry = viewModel::loadLibrary,
        )
        is LibraryUiState.Success -> {
            BackHandler(enabled = current.stack.size > 1) {
                viewModel.navigateUp()
            }
            LibrarySuccessContent(
                stack = current.stack,
                onDirectorySelected = viewModel::enterDirectory,
                onFileSelected = { node -> onPlayFile(node.relativePath) },
            )
        }
    }
}

@Composable
private fun LoadingContent() {
    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center,
    ) {
        CircularProgressIndicator()
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun ErrorContent(message: String, onRetry: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(text = message, color = Color.Red)
        Spacer(modifier = Modifier.height(16.dp))
        Surface(onClick = onRetry) {
            Text(
                text = "Retry",
                modifier = Modifier.padding(horizontal = 24.dp, vertical = 12.dp),
            )
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun LibrarySuccessContent(
    stack: List<DirectoryLevel>,
    onDirectorySelected: (FileNode) -> Unit,
    onFileSelected: (FileNode) -> Unit,
) {
    val currentLevel = stack.last()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
    ) {
        LibraryHeader(stack)

        Spacer(modifier = Modifier.height(16.dp))

        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            items(currentLevel.items, key = { it.relativePath.ifEmpty { it.name } }) { node ->
                LibraryItem(
                    node = node,
                    onClick = {
                        when (node.type) {
                            NodeType.DIRECTORY -> onDirectorySelected(node)
                            NodeType.FILE -> onFileSelected(node)
                        }
                    },
                )
            }
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun LibraryHeader(stack: List<DirectoryLevel>) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        stack.forEachIndexed { index, level ->
            if (index > 0) {
                Text(
                    text = " › ",
                    color = Color.Gray,
                    fontSize = 18.sp,
                )
            }
            val isLast = index == stack.lastIndex
            Text(
                text = level.title,
                fontSize = 18.sp,
                color = if (isLast) Color.White else Color.Gray,
            )
        }
    }
}

@OptIn(ExperimentalTvMaterial3Api::class)
@Composable
private fun LibraryItem(node: FileNode, onClick: () -> Unit) {
    Surface(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Text(
                text = if (node.type == NodeType.DIRECTORY) "\uD83D\uDCC1" else "\u25B6",
                fontSize = 16.sp,
            )

            Spacer(modifier = Modifier.width(12.dp))

            Text(
                text = node.name,
                modifier = Modifier.weight(1f),
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )

            when (node.type) {
                NodeType.FILE -> {
                    node.sizeBytes?.let { size ->
                        Spacer(modifier = Modifier.width(12.dp))
                        Text(
                            text = formatFileSize(size),
                            color = Color.Gray,
                            fontSize = 14.sp,
                        )
                    }
                }
                NodeType.DIRECTORY -> {
                    Spacer(modifier = Modifier.width(12.dp))
                    Text(
                        text = "›",
                        color = Color.Gray,
                        fontSize = 18.sp,
                    )
                }
            }
        }
    }
}

private fun formatFileSize(bytes: Long): String = when {
    bytes >= 1_000_000_000 -> "%.1f GB".format(bytes / 1_000_000_000.0)
    bytes >= 1_000_000 -> "%.1f MB".format(bytes / 1_000_000.0)
    bytes >= 1_000 -> "%.1f KB".format(bytes / 1_000.0)
    else -> "$bytes B"
}
